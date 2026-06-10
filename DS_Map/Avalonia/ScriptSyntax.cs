using System.IO;
using System.Xml;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Provides an AvaloniaEdit <see cref="IHighlightingDefinition"/> for DSPRE script text
    /// (the Scripts / Functions / Actions sections produced by the script editor). It colours
    /// section headers (<c>Script 0:</c>), cross-references (<c>Function_#3</c>), common
    /// control-flow keywords, and numeric / hex literals. The command grammar is open-ended,
    /// so unrecognised command names are left in the default foreground.
    /// </summary>
    public static class ScriptSyntax
    {
        private static IHighlightingDefinition _cached;

        private const string Xshd = @"<?xml version='1.0'?>
<SyntaxDefinition name='DSPREScript' xmlns='http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008'>
  <Color name='Header'  foreground='#4FC1FF' fontWeight='bold'/>
  <Color name='Ref'     foreground='#4EC9B0'/>
  <Color name='Number'  foreground='#B5CEA8'/>
  <Color name='Keyword' foreground='#C586C0' fontWeight='bold'/>

  <RuleSet ignoreCase='true'>
    <Rule color='Header'>^\s*(Script|Function|Action)\s+\d+\s*:</Rule>

    <Keywords color='Keyword'>
      <Word>End</Word>
      <Word>Return</Word>
      <Word>Jump</Word>
      <Word>Call</Word>
      <Word>If</Word>
      <Word>Compare</Word>
      <Word>CompareLastResultJump</Word>
      <Word>CompareLastResultCall</Word>
      <Word>UseScript</Word>
      <Word>Nop</Word>
      <Word>Lock</Word>
      <Word>Release</Word>
      <Word>WaitMoment</Word>
    </Keywords>

    <Rule color='Ref'>(Script|Function|Action|UseScript)_#\d+</Rule>
    <Rule color='Number'>\b0[xX][0-9a-fA-F]+\b</Rule>
    <Rule color='Number'>\b\d+\b</Rule>
  </RuleSet>
</SyntaxDefinition>";

        public static IHighlightingDefinition Definition
        {
            get
            {
                if (_cached != null) return _cached;
                try
                {
                    using var reader = XmlReader.Create(new StringReader(Xshd));
                    _cached = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
                catch { _cached = null; }
                return _cached;
            }
        }
    }
}
