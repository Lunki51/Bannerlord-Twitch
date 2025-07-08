﻿using System.Collections.Generic;
using BannerlordTwitch.Util;

namespace BannerlordTwitch.Localization
{
    public static class Naming
    {
        public static readonly string To = "{=2Ncpkenz}→".Translate();
        public static readonly string Inc = "{=83uNsc2Z}+".Translate();
        public static readonly string Dec = "{=drHSxMvO}−".Translate();
        public static readonly string Gold = "{=BolFnYCO}⦷".Translate();
        public static readonly string XP = "{=JFCeRYdn}XP".Translate();
        public static readonly string HP = "{=NtqSb7B7}HP".Translate();
        public static readonly string Lvl = "{=RQlq59mz}lvl".Translate();
        public static readonly string Item = "{=5rErcMGl}Item".Translate();
        public static readonly string Skills = "{=5gVLi7NA}Skills".Translate();
        public static readonly string Sep = "{=aG3roJj3} ■".Translate();
        public static readonly string Sep2 = "{=AmuKxrmY} ■".Translate();

        public static string NotEnoughGold(int need, int have) =>
            "{=fuwuk4bR}Not enough {GoldIcon}: need {NeededGold}{GoldIcon}, have {HaveGold}{GoldIcon}!"
                .Translate(
                    ("GoldIcon", Gold),
                    ("NeededGold", need),
                    ("HaveGold", have)
                    );

        public static string JoinList(IEnumerable<string> list) => string.Join(Sep, list);
    }
}
