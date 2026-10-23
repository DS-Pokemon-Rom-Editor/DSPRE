using System.IO;
using System.Xml;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace DSPRE.Avalonia
{
    /// <summary>JSON highlighting for the Trainer sprite editor's animation text.</summary>
    public static class JsonSyntax
    {
        private static IHighlightingDefinition _cached;

        private const string Xshd = @"<?xml version='1.0'?>
<SyntaxDefinition name='Json' xmlns='http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008'>
  <Color name='Key'         foreground='#9CDCFE'/>
  <Color name='String'      foreground='#CE9178'/>
  <Color name='Number'      foreground='#B5CEA8'/>
  <Color name='Keyword'     foreground='#569CD6' fontWeight='bold'/>
  <Color name='Punctuation' foreground='#D4D4D4'/>

  <RuleSet>
    <Rule color='Key'>&quot;[^&quot;\\]*(\\.[^&quot;\\]*)*&quot;\s*(?=:)</Rule>
    <Rule color='String'>&quot;[^&quot;\\]*(\\.[^&quot;\\]*)*&quot;</Rule>
    <Rule color='Number'>\b-?\d+(\.\d+)?([eE][+-]?\d+)?\b</Rule>
    <Rule color='Punctuation'>[\{\}\[\]:,]</Rule>

    <Keywords color='Keyword'>
      <Word>true</Word>
      <Word>false</Word>
      <Word>null</Word>
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
                catch (System.Exception ex) { AppLogger.WarnOnce("JsonSyntax.Definition", ex); _cached = null; }
                return _cached;
            }
        }
    }
}
