using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace DSPRE {
    /// <summary>
    /// Draws the small slice of Markdown the changelogs actually use into a RichTextBox: headings,
    /// bullets, rules, bold, italics and links. Not a general parser, and deliberately not a
    /// dependency, since the changelogs are the only thing being rendered.
    /// </summary>
    public static class MarkdownRichText {
        private static readonly Regex LinkPattern = new Regex(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled);
        private static readonly Regex BoldPattern = new Regex(@"\*\*([^*]+)\*\*", RegexOptions.Compiled);
        private static readonly Regex ItalicPattern = new Regex(@"(?<!\*)\*([^*]+)\*(?!\*)", RegexOptions.Compiled);
        private static readonly Regex CodePattern = new Regex(@"`([^`]+)`", RegexOptions.Compiled);

        public static void Render(RichTextBox box, string markdown) {
            box.Clear();
            if (string.IsNullOrEmpty(markdown)) {
                return;
            }

            Font baseFont = box.Font;
            box.ReadOnly = true;
            box.DetectUrls = true;

            foreach (Block block in Parse(markdown)) {
                switch (block.Kind) {
                    case BlockKind.Heading1:
                        Append(box, block.Text, new Font(baseFont.FontFamily, baseFont.Size + 5f, FontStyle.Bold), false);
                        break;
                    case BlockKind.Heading2:
                        Append(box, block.Text, new Font(baseFont.FontFamily, baseFont.Size + 3f, FontStyle.Bold), false);
                        break;
                    case BlockKind.Heading3:
                        Append(box, block.Text, new Font(baseFont.FontFamily, baseFont.Size + 1f, FontStyle.Bold), false);
                        break;
                    case BlockKind.Bullet:
                        Append(box, block.Text, baseFont, true);
                        break;
                    case BlockKind.Rule:
                        box.SelectionBullet = false;
                        box.SelectionFont = baseFont;
                        box.SelectionColor = SystemColors.ControlDark;
                        box.AppendText(new string('_', 60) + Environment.NewLine + Environment.NewLine);
                        break;
                    default:
                        Append(box, block.Text, baseFont, false);
                        break;
                }
            }

            box.SelectionStart = 0;
            box.ScrollToCaret();
        }

        private static void Append(RichTextBox box, string text, Font font, bool bullet) {
            box.SelectionBullet = bullet;
            box.SelectionIndent = bullet ? 12 : 0;
            box.SelectionFont = font;
            box.SelectionColor = box.ForeColor;
            AppendInline(box, text, font);
            box.AppendText(Environment.NewLine);
            if (!bullet) {
                box.AppendText(Environment.NewLine);
            }
            box.SelectionBullet = false;
            box.SelectionIndent = 0;
        }

        // Walks the run looking for the inline markers, so bold and italic keep the surrounding style.
        private static void AppendInline(RichTextBox box, string text, Font font) {
            string remaining = text;
            while (remaining.Length > 0) {
                Match bold = BoldPattern.Match(remaining);
                Match italic = ItalicPattern.Match(remaining);
                Match code = CodePattern.Match(remaining);

                Match first = null;
                FontStyle style = FontStyle.Regular;
                foreach (Match candidate in new[] { bold, italic, code }) {
                    if (!candidate.Success) {
                        continue;
                    }
                    if (first == null || candidate.Index < first.Index) {
                        first = candidate;
                        style = candidate == bold ? FontStyle.Bold
                              : candidate == italic ? FontStyle.Italic
                              : FontStyle.Regular;
                    }
                }

                if (first == null) {
                    box.SelectionFont = font;
                    box.AppendText(remaining);
                    return;
                }

                if (first.Index > 0) {
                    box.SelectionFont = font;
                    box.AppendText(remaining.Substring(0, first.Index));
                }

                box.SelectionFont = new Font(font, font.Style | style);
                box.AppendText(first.Groups[1].Value);
                box.SelectionFont = font;
                remaining = remaining.Substring(first.Index + first.Length);
            }
        }

        private enum BlockKind { Paragraph, Heading1, Heading2, Heading3, Bullet, Rule }

        private struct Block {
            public BlockKind Kind;
            public string Text;
            public Block(BlockKind kind, string text) { Kind = kind; Text = text; }
        }

        private static List<Block> Parse(string markdown) {
            List<Block> blocks = new List<Block>();
            string[] lines = markdown.Replace("\r\n", "\n").Split('\n');
            string pending = null;
            BlockKind pendingKind = BlockKind.Paragraph;

            foreach (string raw in lines) {
                string line = raw.TrimEnd();
                string trimmed = line.TrimStart();

                if (trimmed.Length == 0) {
                    Flush(blocks, ref pending, ref pendingKind);
                    continue;
                }

                if (trimmed.StartsWith("---")) {
                    Flush(blocks, ref pending, ref pendingKind);
                    blocks.Add(new Block(BlockKind.Rule, string.Empty));
                    continue;
                }

                if (trimmed.StartsWith("### ")) {
                    Flush(blocks, ref pending, ref pendingKind);
                    blocks.Add(new Block(BlockKind.Heading3, Inline(trimmed.Substring(4))));
                    continue;
                }
                if (trimmed.StartsWith("## ")) {
                    Flush(blocks, ref pending, ref pendingKind);
                    blocks.Add(new Block(BlockKind.Heading2, Inline(trimmed.Substring(3))));
                    continue;
                }
                if (trimmed.StartsWith("# ")) {
                    Flush(blocks, ref pending, ref pendingKind);
                    blocks.Add(new Block(BlockKind.Heading1, Inline(trimmed.Substring(2))));
                    continue;
                }

                if (trimmed.StartsWith("- ") || trimmed.StartsWith("* ")) {
                    Flush(blocks, ref pending, ref pendingKind);
                    pending = Inline(trimmed.Substring(2));
                    pendingKind = BlockKind.Bullet;
                    continue;
                }

                // A wrapped continuation of whatever block is open.
                if (pending != null) {
                    pending += " " + Inline(trimmed);
                } else {
                    pending = Inline(trimmed);
                    pendingKind = BlockKind.Paragraph;
                }
            }

            Flush(blocks, ref pending, ref pendingKind);
            return blocks;
        }

        private static void Flush(List<Block> blocks, ref string pending, ref BlockKind kind) {
            if (pending != null) {
                blocks.Add(new Block(kind, pending));
                pending = null;
                kind = BlockKind.Paragraph;
            }
        }

        // Links become "text (url)" so the address stays visible and RichTextBox turns it into a real link.
        private static string Inline(string text) {
            return LinkPattern.Replace(text, m => {
                string label = m.Groups[1].Value;
                string url = m.Groups[2].Value;
                return label == url ? url : label + " (" + url + ")";
            });
        }
    }
}
