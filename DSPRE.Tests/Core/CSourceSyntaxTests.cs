using System.Linq;
using DSPRE.HgEngine;
using Xunit;

namespace DSPRE.Tests.Core
{
    /// <summary>
    /// The C reader hg-engine tables go through. Each case is source someone could legally write that a pattern
    /// match would misread: braces in strings and comments, conditional rows, symbolic designators, casts.
    /// </summary>
    public class CSourceSyntaxTests
    {
        [Fact]
        public void BracesAndCommasInsideStringsAndCommentsAreNotCode()
        {
            const string src = "const char *s[] = { \"a, {b}\", /* }, */ \"c\" // }\n, 'x' };";
            var decl = new CSourceFile(src).Find("s");

            Assert.NotNull(decl);
            Assert.Equal(new[] { "\"a, {b}\"", "\"c\"", "'x'" }, decl.Init.Items.Select(i => i.ValueText(src)));
        }

        [Fact]
        public void SymbolicIndexDesignatorsKeepTheirNameAndValue()
        {
            const string src = "static const u16 t[] = {\n    [ABILITY_STENCH] = { .a = TRUE },\n    [3] = 7,\n    9,\n};\n";
            var items = new CSourceFile(src).Find("t").Init.Items;

            Assert.Equal("ABILITY_STENCH", items[0].IndexText);
            Assert.Equal(-1, items[0].Position);
            Assert.Equal("TRUE", items[0].List.Field("a").ValueText(src));
            Assert.Equal(3, items[1].Position);
            Assert.Equal("7", items[1].ValueText(src));
            Assert.Equal(4, items[2].Position);
        }

        [Fact]
        public void RowsUnderAPreprocessorConditionAreMarkedAndNotMergedIntoTheirNeighbours()
        {
            const string src = "const u8 t[][2] = {\n    { 1, 2 },\n#if FAIRY_TYPE_IMPLEMENTED\n    { 3, 4 },\n#else\n    { 5, 6 },\n#endif\n    { 7, 8 },\n};\n";
            var items = new CSourceFile(src).Find("t").Init.Items;

            Assert.Equal(4, items.Count);
            Assert.Empty(items[0].Conditions);
            Assert.Equal("FAIRY_TYPE_IMPLEMENTED", Assert.Single(items[1].Conditions));
            Assert.Equal("!(FAIRY_TYPE_IMPLEMENTED)", Assert.Single(items[2].Conditions));
            Assert.Empty(items[3].Conditions);
            Assert.Equal("{ 7, 8 }", items[3].ValueText(src));
        }

        [Fact]
        public void DeclarationsAreFoundByNameWithTheirDimensionsAndFunctionBodiesAreSkipped()
        {
            const string src = "#define N 2\nvoid f(void)\n{\n    int local[] = { 1 };\n}\nconst u16 sLut[N][2] = {\n    { MAP_R01, SWARM_GRASS }, // first\n    { MAP_R03, SWARM_SURFING },\n};\n";
            var file = new CSourceFile(src);

            Assert.Null(file.Find("local"));
            var decl = file.Find("sLut");
            Assert.Equal(new[] { "[N]", "[2]" }, decl.Dimensions);
            Assert.Equal(2, decl.Init.Items.Count);
            Assert.Equal("SWARM_SURFING", decl.Init.Items[1].List.Items[1].ValueText(src));
            Assert.Equal(';', src[decl.End - 1]);
        }

        [Fact]
        public void CastsAndSizeofExpressionsStayOneValue()
        {
            const string src = "const PokedexArchiveMember m[] = {\n    [0] = { (const u8 *)sA, sizeof(sA) / sizeof(*sA) },\n};\n";
            var row = new CSourceFile(src).Find("m").Init.Items[0].List;

            Assert.Equal(2, row.Items.Count);
            Assert.Equal("(const u8 *)sA", row.Items[0].ValueText(src));
            Assert.Equal("sizeof(sA) / sizeof(*sA)", row.Items[1].ValueText(src));
        }

        [Fact]
        public void ADirectiveOnlyStartsAtTheBeginningOfALineAndRunsThroughContinuations()
        {
            const string src = "#define LONG(a) \\\n    ((a) + 1)\nint x = a # b;\n";
            var tokens = CLexer.Tokenize(src);

            Assert.Equal(CTokenKind.Directive, tokens[0].Kind);
            Assert.EndsWith("((a) + 1)", tokens[0].Text(src));
            Assert.Contains(tokens, t => t.Kind == CTokenKind.Punct && t.Text(src) == "#");
        }

        [Fact]
        public void ARangeReadsOneEntryOfALargerFile()
        {
            const string src = "const T t[] = {\n    [SPECIES_A] = { .x = 1, .y = { 2, 3 } },\n    [SPECIES_B] = { .x = 4 },\n};\n";
            int open = src.IndexOf("{ .x = 4");
            int close = src.IndexOf('}', open);
            var list = new CSourceFile(src, open, close + 1).ListAt(open);

            Assert.Equal("4", list.Field("x").ValueText(src));
            Assert.Single(list.Items);
        }

        [Fact]
        public void ConditionsEvaluateLikeThePreprocessor()
        {
            var defined = new System.Collections.Generic.HashSet<string> { "FAIRY_TYPE_IMPLEMENTED", "EXPAND_ROAMERS" };
            int? Value(string n) => n == "FAIRY_TYPE_IMPLEMENTED" ? 1 : n == "GEN" ? 6 : null;

            Assert.True(CConditions.Evaluate(new[] { "FAIRY_TYPE_IMPLEMENTED" }, defined.Contains, Value));
            Assert.False(CConditions.Evaluate(new[] { "!(FAIRY_TYPE_IMPLEMENTED)" }, defined.Contains, Value));
            Assert.True(CConditions.Evaluate(new[] { "defined(EXPAND_ROAMERS)", "GEN >= 6" }, defined.Contains, Value));
            // An undefined name is 0, as in C.
            Assert.False(CConditions.Evaluate(new[] { "UNDEFINED_SWITCH" }, defined.Contains, Value));
            Assert.Null(CConditions.Evaluate(new[] { "SOME_MACRO(3)" }, defined.Contains, Value));
        }

        [Fact]
        public void ATrailingCommentIsNotPartOfItsDirective()
        {
            const string src = "#define X 5 // five\n/* #define Y 6 */\n";
            var tokens = CLexer.Tokenize(src, keepComments: true);

            Assert.Equal("#define X 5 ", tokens[0].Text(src));
            Assert.Equal(CTokenKind.Comment, tokens[1].Kind);
            Assert.Equal(CTokenKind.Comment, tokens[2].Kind);
            Assert.Equal(3, tokens.Count);
        }

        [Fact]
        public void NormalizeComparesSpellingsWithoutCommentsOrLayout()
        {
            Assert.Equal(CSourceFile.Normalize("F_A | /* x */ F_B"), CSourceFile.Normalize("F_A|F_B // y"));
            Assert.NotEqual(CSourceFile.Normalize("F_A | F_B"), CSourceFile.Normalize("F_B | F_A"));
        }
    }
}
