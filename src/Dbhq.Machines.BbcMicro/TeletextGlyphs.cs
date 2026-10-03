// The glyph rows below are data taken from Bedstead's glyphs[] table (bedstead.c, version 3.261),
// the English (SAA5050) set: the US ASCII (SAA5055) entries for codes $20 to $7F, with the twelve
// codes where the English set differs taken from its "Extra characters found in the English
// (SAA5050) character set" entries. Only the row data was taken, never Bedstead's code. Where it
// came from, when, and the SHA-256 of the file read are in NOTICE.md at the repository root.
// Bedstead's header, as it stands in that file:
//
//   Many of the character bitmaps below formed the typeface embodied in
//   the SAA5050 series of character-generator chips originally made and
//   sold by British company Mullard in the early 1980s.  Copyright in the
//   typeface will still be owned by Mullard's corporate successors, but
//   under section 55 of the Copyright Designs and Patents Act 1988 that
//   copyright is no longer infringed by the production or use of
//   articles specifically designed or adapted for producing material in
//   that typeface.
//
//   The rest of the glyphs, and all of the code in this file, were
//   written by Ben Harris <bjh21@bjh21.me.uk>, Simon Tatham
//   <anakin@pobox.com>, Marnanel Thurman <marnanel@thurman.org.uk> and
//   Neil Williamson <p298@tiddles.org> between 2009 and 2025.
//
//   To the extent possible under law, Ben Harris, Simon Tatham, Marnanel
//   Thurman and Neil Williamson have dedicated all copyright and related
//   and neighboring rights to this software and the embodied typeface to
//   the public domain worldwide.  This software and typeface are
//   distributed without any warranty.
//
//   You should have received a copy of the CC0 Public Domain Dedication
//   along with this software. If not, see
//   <http://creativecommons.org/publicdomain/zero/1.0/>.
namespace Dbhq.Machines.BbcMicro;

/// <summary>
/// The SAA5050's alphanumeric character set (English), as the rows of each character's 5 by 9
/// dot matrix: <c>video.md</c> s4.1 and s4.4, the datasheet's Figure 11.
/// </summary>
/// <remarks>
/// <para>
/// A character cell is 6 dots by 10 rows. The chip's ROM holds only the 5 by 9 matrix to the right
/// of the cell's left column and below its top row, both of which are always background
/// (<c>video.md</c> s4.1, from the datasheet and Bedstead's own note, "Top row (and left column)
/// don't appear in ROM"). So row 0 of a glyph here is row 1 of the cell, and bit 4 of a row is the
/// cell's second column, bit 0 its sixth.
/// </para>
/// <para>
/// The English set is US ASCII except at <c>$23</c> (pound), <c>$27</c> (a plain tick),
/// <c>$5B</c> to <c>$5E</c> (left arrow, one half, right arrow, up arrow), <c>$5F</c> (hash),
/// <c>$60</c> (long dash) and <c>$7B</c> to <c>$7E</c> (one quarter, double bar, three quarters,
/// divide), <c>video.md</c> s4.4. The mosaic graphics are not in this table: the chip decodes them
/// from the code's bits (the datasheet's Figure 9), and so does <see cref="Teletext"/>.
/// </para>
/// <para>
/// <c>TeletextGlyphsTests</c> holds independent copies of a set of these rows read from the
/// datasheet's Figure 11 and checks this table against them.
/// </para>
/// </remarks>
public static class TeletextGlyphs
{
    /// <summary>The rows of a glyph: nine.</summary>
    public const int RowsPerGlyph = 9;

    // Nine rows per code, $20 to $7F; bit 4 is the leftmost dot.
    private static ReadOnlySpan<byte> Table =>
    [
        0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, // $20 space (U+0020)
        0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00000, 0b00100, 0b00000, 0b00000, // $21 exclam (U+0021)
        0b01010, 0b01010, 0b01010, 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, // $22 quotedbl (U+0022)
        0b00110, 0b01001, 0b01000, 0b11100, 0b01000, 0b01000, 0b11111, 0b00000, 0b00000, // $23 sterling (U+00A3)
        0b01110, 0b10101, 0b10100, 0b01110, 0b00101, 0b10101, 0b01110, 0b00000, 0b00000, // $24 dollar (U+0024)
        0b11000, 0b11001, 0b00010, 0b00100, 0b01000, 0b10011, 0b00011, 0b00000, 0b00000, // $25 percent (U+0025)
        0b01000, 0b10100, 0b10100, 0b01000, 0b10101, 0b10010, 0b01101, 0b00000, 0b00000, // $26 ampersand (U+0026)
        0b00100, 0b00100, 0b00100, 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, // $27 quotesingle (U+0027)
        0b00010, 0b00100, 0b01000, 0b01000, 0b01000, 0b00100, 0b00010, 0b00000, 0b00000, // $28 parenleft (U+0028)
        0b01000, 0b00100, 0b00010, 0b00010, 0b00010, 0b00100, 0b01000, 0b00000, 0b00000, // $29 parenright (U+0029)
        0b00100, 0b10101, 0b01110, 0b00100, 0b01110, 0b10101, 0b00100, 0b00000, 0b00000, // $2A asterisk (U+002A)
        0b00000, 0b00100, 0b00100, 0b11111, 0b00100, 0b00100, 0b00000, 0b00000, 0b00000, // $2B plus (U+002B)
        0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b00100, 0b00100, 0b01000, 0b00000, // $2C comma (U+002C)
        0b00000, 0b00000, 0b00000, 0b01110, 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, // $2D hyphen (U+002D)
        0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b00100, 0b00000, 0b00000, // $2E period (U+002E)
        0b00000, 0b00001, 0b00010, 0b00100, 0b01000, 0b10000, 0b00000, 0b00000, 0b00000, // $2F slash (U+002F)
        0b00100, 0b01010, 0b10001, 0b10001, 0b10001, 0b01010, 0b00100, 0b00000, 0b00000, // $30 zero (U+0030)
        0b00100, 0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110, 0b00000, 0b00000, // $31 one (U+0031)
        0b01110, 0b10001, 0b00001, 0b00110, 0b01000, 0b10000, 0b11111, 0b00000, 0b00000, // $32 two (U+0032)
        0b11111, 0b00001, 0b00010, 0b00110, 0b00001, 0b10001, 0b01110, 0b00000, 0b00000, // $33 three (U+0033)
        0b00010, 0b00110, 0b01010, 0b10010, 0b11111, 0b00010, 0b00010, 0b00000, 0b00000, // $34 four (U+0034)
        0b11111, 0b10000, 0b11110, 0b00001, 0b00001, 0b10001, 0b01110, 0b00000, 0b00000, // $35 five (U+0035)
        0b00110, 0b01000, 0b10000, 0b11110, 0b10001, 0b10001, 0b01110, 0b00000, 0b00000, // $36 six (U+0036)
        0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b01000, 0b01000, 0b00000, 0b00000, // $37 seven (U+0037)
        0b01110, 0b10001, 0b10001, 0b01110, 0b10001, 0b10001, 0b01110, 0b00000, 0b00000, // $38 eight (U+0038)
        0b01110, 0b10001, 0b10001, 0b01111, 0b00001, 0b00010, 0b01100, 0b00000, 0b00000, // $39 nine (U+0039)
        0b00000, 0b00000, 0b00100, 0b00000, 0b00000, 0b00000, 0b00100, 0b00000, 0b00000, // $3A colon (U+003A)
        0b00000, 0b00000, 0b00100, 0b00000, 0b00000, 0b00100, 0b00100, 0b01000, 0b00000, // $3B semicolon (U+003B)
        0b00010, 0b00100, 0b01000, 0b10000, 0b01000, 0b00100, 0b00010, 0b00000, 0b00000, // $3C less (U+003C)
        0b00000, 0b00000, 0b11111, 0b00000, 0b11111, 0b00000, 0b00000, 0b00000, 0b00000, // $3D equal (U+003D)
        0b01000, 0b00100, 0b00010, 0b00001, 0b00010, 0b00100, 0b01000, 0b00000, 0b00000, // $3E greater (U+003E)
        0b01110, 0b10001, 0b00010, 0b00100, 0b00100, 0b00000, 0b00100, 0b00000, 0b00000, // $3F question (U+003F)
        0b01110, 0b10001, 0b10111, 0b10101, 0b10111, 0b10000, 0b01110, 0b00000, 0b00000, // $40 at (U+0040)
        0b00100, 0b01010, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b00000, 0b00000, // $41 A (U+0041)
        0b11110, 0b10001, 0b10001, 0b11110, 0b10001, 0b10001, 0b11110, 0b00000, 0b00000, // $42 B (U+0042)
        0b01110, 0b10001, 0b10000, 0b10000, 0b10000, 0b10001, 0b01110, 0b00000, 0b00000, // $43 C (U+0043)
        0b11110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b11110, 0b00000, 0b00000, // $44 D (U+0044)
        0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b11111, 0b00000, 0b00000, // $45 E (U+0045)
        0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b10000, 0b00000, 0b00000, // $46 F (U+0046)
        0b01110, 0b10001, 0b10000, 0b10000, 0b10011, 0b10001, 0b01111, 0b00000, 0b00000, // $47 G (U+0047)
        0b10001, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001, 0b00000, 0b00000, // $48 H (U+0048)
        0b01110, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110, 0b00000, 0b00000, // $49 I (U+0049)
        0b00001, 0b00001, 0b00001, 0b00001, 0b00001, 0b10001, 0b01110, 0b00000, 0b00000, // $4A J (U+004A)
        0b10001, 0b10010, 0b10100, 0b11000, 0b10100, 0b10010, 0b10001, 0b00000, 0b00000, // $4B K (U+004B)
        0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b11111, 0b00000, 0b00000, // $4C L (U+004C)
        0b10001, 0b11011, 0b10101, 0b10101, 0b10001, 0b10001, 0b10001, 0b00000, 0b00000, // $4D M (U+004D)
        0b10001, 0b10001, 0b11001, 0b10101, 0b10011, 0b10001, 0b10001, 0b00000, 0b00000, // $4E N (U+004E)
        0b01110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110, 0b00000, 0b00000, // $4F O (U+004F)
        0b11110, 0b10001, 0b10001, 0b11110, 0b10000, 0b10000, 0b10000, 0b00000, 0b00000, // $50 P (U+0050)
        0b01110, 0b10001, 0b10001, 0b10001, 0b10101, 0b10010, 0b01101, 0b00000, 0b00000, // $51 Q (U+0051)
        0b11110, 0b10001, 0b10001, 0b11110, 0b10100, 0b10010, 0b10001, 0b00000, 0b00000, // $52 R (U+0052)
        0b01110, 0b10001, 0b10000, 0b01110, 0b00001, 0b10001, 0b01110, 0b00000, 0b00000, // $53 S (U+0053)
        0b11111, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00000, 0b00000, // $54 T (U+0054)
        0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110, 0b00000, 0b00000, // $55 U (U+0055)
        0b10001, 0b10001, 0b10001, 0b01010, 0b01010, 0b00100, 0b00100, 0b00000, 0b00000, // $56 V (U+0056)
        0b10001, 0b10001, 0b10001, 0b10101, 0b10101, 0b10101, 0b01010, 0b00000, 0b00000, // $57 W (U+0057)
        0b10001, 0b10001, 0b01010, 0b00100, 0b01010, 0b10001, 0b10001, 0b00000, 0b00000, // $58 X (U+0058)
        0b10001, 0b10001, 0b01010, 0b00100, 0b00100, 0b00100, 0b00100, 0b00000, 0b00000, // $59 Y (U+0059)
        0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b10000, 0b11111, 0b00000, 0b00000, // $5A Z (U+005A)
        0b00000, 0b00100, 0b01000, 0b11111, 0b01000, 0b00100, 0b00000, 0b00000, 0b00000, // $5B arrowleft (U+2190)
        0b10000, 0b10000, 0b10000, 0b10000, 0b10110, 0b00001, 0b00010, 0b00100, 0b00111, // $5C onehalf (U+00BD)
        0b00000, 0b00100, 0b00010, 0b11111, 0b00010, 0b00100, 0b00000, 0b00000, 0b00000, // $5D arrowright (U+2192)
        0b00000, 0b00100, 0b01110, 0b10101, 0b00100, 0b00100, 0b00000, 0b00000, 0b00000, // $5E arrowup (U+2191)
        0b01010, 0b01010, 0b11111, 0b01010, 0b11111, 0b01010, 0b01010, 0b00000, 0b00000, // $5F numbersign (U+0023)
        0b00000, 0b00000, 0b00000, 0b11111, 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, // $60 emdash (U+2014)
        0b00000, 0b00000, 0b01110, 0b00001, 0b01111, 0b10001, 0b01111, 0b00000, 0b00000, // $61 a (U+0061)
        0b10000, 0b10000, 0b11110, 0b10001, 0b10001, 0b10001, 0b11110, 0b00000, 0b00000, // $62 b (U+0062)
        0b00000, 0b00000, 0b01111, 0b10000, 0b10000, 0b10000, 0b01111, 0b00000, 0b00000, // $63 c (U+0063)
        0b00001, 0b00001, 0b01111, 0b10001, 0b10001, 0b10001, 0b01111, 0b00000, 0b00000, // $64 d (U+0064)
        0b00000, 0b00000, 0b01110, 0b10001, 0b11111, 0b10000, 0b01110, 0b00000, 0b00000, // $65 e (U+0065)
        0b00010, 0b00100, 0b00100, 0b01110, 0b00100, 0b00100, 0b00100, 0b00000, 0b00000, // $66 f (U+0066)
        0b00000, 0b00000, 0b01111, 0b10001, 0b10001, 0b10001, 0b01111, 0b00001, 0b01110, // $67 g (U+0067)
        0b10000, 0b10000, 0b11110, 0b10001, 0b10001, 0b10001, 0b10001, 0b00000, 0b00000, // $68 h (U+0068)
        0b00100, 0b00000, 0b01100, 0b00100, 0b00100, 0b00100, 0b01110, 0b00000, 0b00000, // $69 i (U+0069)
        0b00100, 0b00000, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01000, // $6A j (U+006A)
        0b01000, 0b01000, 0b01001, 0b01010, 0b01100, 0b01010, 0b01001, 0b00000, 0b00000, // $6B k (U+006B)
        0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110, 0b00000, 0b00000, // $6C l (U+006C)
        0b00000, 0b00000, 0b11010, 0b10101, 0b10101, 0b10101, 0b10101, 0b00000, 0b00000, // $6D m (U+006D)
        0b00000, 0b00000, 0b11110, 0b10001, 0b10001, 0b10001, 0b10001, 0b00000, 0b00000, // $6E n (U+006E)
        0b00000, 0b00000, 0b01110, 0b10001, 0b10001, 0b10001, 0b01110, 0b00000, 0b00000, // $6F o (U+006F)
        0b00000, 0b00000, 0b11110, 0b10001, 0b10001, 0b10001, 0b11110, 0b10000, 0b10000, // $70 p (U+0070)
        0b00000, 0b00000, 0b01111, 0b10001, 0b10001, 0b10001, 0b01111, 0b00001, 0b00001, // $71 q (U+0071)
        0b00000, 0b00000, 0b01011, 0b01100, 0b01000, 0b01000, 0b01000, 0b00000, 0b00000, // $72 r (U+0072)
        0b00000, 0b00000, 0b01111, 0b10000, 0b01110, 0b00001, 0b11110, 0b00000, 0b00000, // $73 s (U+0073)
        0b00100, 0b00100, 0b01110, 0b00100, 0b00100, 0b00100, 0b00010, 0b00000, 0b00000, // $74 t (U+0074)
        0b00000, 0b00000, 0b10001, 0b10001, 0b10001, 0b10001, 0b01111, 0b00000, 0b00000, // $75 u (U+0075)
        0b00000, 0b00000, 0b10001, 0b10001, 0b01010, 0b01010, 0b00100, 0b00000, 0b00000, // $76 v (U+0076)
        0b00000, 0b00000, 0b10001, 0b10001, 0b10101, 0b10101, 0b01010, 0b00000, 0b00000, // $77 w (U+0077)
        0b00000, 0b00000, 0b10001, 0b01010, 0b00100, 0b01010, 0b10001, 0b00000, 0b00000, // $78 x (U+0078)
        0b00000, 0b00000, 0b10001, 0b10001, 0b10001, 0b10001, 0b01111, 0b00001, 0b01110, // $79 y (U+0079)
        0b00000, 0b00000, 0b11111, 0b00010, 0b00100, 0b01000, 0b11111, 0b00000, 0b00000, // $7A z (U+007A)
        0b01000, 0b01000, 0b01000, 0b01000, 0b01001, 0b00011, 0b00101, 0b00111, 0b00001, // $7B onequarter (U+00BC)
        0b01010, 0b01010, 0b01010, 0b01010, 0b01010, 0b01010, 0b01010, 0b00000, 0b00000, // $7C dblverticalbar (U+2016)
        0b11000, 0b00100, 0b11000, 0b00100, 0b11001, 0b00011, 0b00101, 0b00111, 0b00001, // $7D threequarters (U+00BE)
        0b00000, 0b00100, 0b00000, 0b11111, 0b00000, 0b00100, 0b00000, 0b00000, 0b00000, // $7E divide (U+00F7)
        0b11111, 0b11111, 0b11111, 0b11111, 0b11111, 0b11111, 0b11111, 0b00000, 0b00000, // $7F filledbox (U+25A0)
    ];

    /// <summary>
    /// The nine rows of the alphanumeric glyph for <paramref name="code"/>, whose bit 7 is
    /// ignored as the chip ignores it; codes below <c>$20</c> are control codes and show as a space.
    /// </summary>
    public static ReadOnlySpan<byte> Rows(int code)
    {
        int c = code & 0x7F;
        return c < 0x20 ? Table[..RowsPerGlyph] : Table.Slice((c - 0x20) * RowsPerGlyph, RowsPerGlyph);
    }
}
