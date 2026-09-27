using System.Drawing;
using System.Windows.Forms;

internal sealed class DarkMenuColors : ProfessionalColorTable
{
    private readonly Color background=Color.FromArgb(34,34,46),hover=Color.FromArgb(55,47,74),line=Color.FromArgb(74,65,93);
    public DarkMenuColors(){UseSystemColors=false;}
    public override Color ToolStripDropDownBackground { get{return background;} }
    public override Color MenuStripGradientBegin { get{return background;} }
    public override Color MenuStripGradientEnd { get{return background;} }
    public override Color ImageMarginGradientBegin { get{return background;} }
    public override Color ImageMarginGradientMiddle { get{return background;} }
    public override Color ImageMarginGradientEnd { get{return background;} }
    public override Color MenuItemSelected { get{return hover;} }
    public override Color MenuItemSelectedGradientBegin { get{return hover;} }
    public override Color MenuItemSelectedGradientEnd { get{return hover;} }
    public override Color MenuItemPressedGradientBegin { get{return hover;} }
    public override Color MenuItemPressedGradientMiddle { get{return hover;} }
    public override Color MenuItemPressedGradientEnd { get{return hover;} }
    public override Color MenuItemBorder { get{return line;} }
    public override Color MenuBorder { get{return line;} }
    public override Color CheckBackground { get{return hover;} }
    public override Color CheckSelectedBackground { get{return hover;} }
    public override Color CheckPressedBackground { get{return hover;} }
    public override Color SeparatorDark { get{return line;} }
    public override Color SeparatorLight { get{return background;} }
}
