using System.IO;
using System.Xml;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace DSPRE.Avalonia
{
    /// <summary>C highlighting for AvaloniaEdit, used by the hg-engine battle tests editor.</summary>
    public static class CSyntax
    {
        private static IHighlightingDefinition _cached;

        private const string Xshd = @"<?xml version='1.0'?>
<SyntaxDefinition name='C' xmlns='http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008'>
  <Color name='Comment'   foreground='#6A9955'/>
  <Color name='String'    foreground='#CE9178'/>
  <Color name='Directive' foreground='#C586C0'/>
  <Color name='Number'    foreground='#B5CEA8'/>
  <Color name='Constant'  foreground='#4FC1FF'/>
  <Color name='Field'     foreground='#9CDCFE'/>
  <Color name='Keyword'   foreground='#569CD6' fontWeight='bold'/>

  <RuleSet>
    <Span color='Comment' begin='//'/>
    <Span color='Comment' multiline='true' begin='/\*' end='\*/'/>
    <Span color='String'>
      <Begin>&quot;</Begin>
      <End>&quot;</End>
      <RuleSet><Span begin='\\' end='.'/></RuleSet>
    </Span>
    <Rule color='Directive'>^\s*\#\s*\w+</Rule>
    <Rule color='Number'>\b(0[xX][0-9A-Fa-f]+|\d+)\b</Rule>
    <Rule color='Field'>\.[a-zA-Z_]\w*</Rule>
    <Rule color='Constant'>\b[A-Z][A-Z0-9]*_[A-Z0-9_]*\b</Rule>
    <Keywords color='Keyword'>
      <Word>struct</Word>
      <Word>const</Word>
      <Word>static</Word>
      <Word>enum</Word>
      <Word>sizeof</Word>
      <Word>TRUE</Word>
      <Word>FALSE</Word>
      <Word>NULL</Word>
    </Keywords>
  </RuleSet>
</SyntaxDefinition>";

        public static IHighlightingDefinition Definition
        {
            get
            {
                if (_cached != null) return _cached;
                try
                {
                    using XmlReader reader = XmlReader.Create(new StringReader(Xshd));
                    _cached = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
                catch { _cached = null; }
                return _cached;
            }
        }
    }
}
