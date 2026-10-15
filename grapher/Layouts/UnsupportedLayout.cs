using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace grapher.Layouts
{
    public class UnsupportedLayout : LayoutBase
    {
        public const string LUTLayoutText = "此界面不支持该模式，详情请参阅指南。";

        public UnsupportedLayout()
            : base()
        {
            Name = "不支持";
            Mode = AccelMode.noaccel + 1;

            GainSwitchOptionLayout = new OptionLayout(false, string.Empty);
            ClassicCapLayout = new OptionLayout(false, string.Empty);
            PowerCapLayout = new OptionLayout(false, string.Empty);
            DecayRateLayout = new OptionLayout(false, string.Empty);
            GammaLayout = new OptionLayout(false, string.Empty);
            SmoothLayout = new OptionLayout(false, string.Empty);
            InputOffsetLayout = new OptionLayout(false, string.Empty);
            LimitLayout = new OptionLayout(false, string.Empty);
            PowerClassicLayout = new OptionLayout(false, string.Empty);
            ExponentLayout = new OptionLayout(false, Exponent);
            OutputOffsetLayout = new OptionLayout(false, string.Empty);
            SyncSpeedLayout = new OptionLayout(false, string.Empty);
            LutTextLayout = new OptionLayout(true, LUTLayoutText);
            LutPanelLayout = new OptionLayout(false, string.Empty);
            LutApplyOptionsLayout = new OptionLayout(false, string.Empty);
            InputJumpLayout = new OptionLayout(false, string.Empty);
            OutputJumpLayout = new OptionLayout(false, string.Empty);
        }
    }
}