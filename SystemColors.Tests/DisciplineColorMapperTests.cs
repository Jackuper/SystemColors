using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace SystemColors.Tests
{
    [TestClass]
    public class DisciplineColorMapperTests
    {
        private DisciplineColorMapper BuildMapper()
        {
            var config = new ColorConfig
            {
                Disciplines = new List<DisciplineEntry>
                {
                    new DisciplineEntry { Codes = new List<string> { "MECH", "MD" }, Color = new[] { 0, 255, 127 } },
                    new DisciplineEntry { Codes = new List<string> { "PLM", "PL" }, Color = new[] { 0, 65, 255 } },
                    new DisciplineEntry { Codes = new List<string> { "FP" },         Color = new[] { 255, 0, 0 } }
                }
            };
            return new DisciplineColorMapper(config);
        }

        private static SystemEntry MakeSystem(string name, int[] color, params string[] keywords) =>
            new SystemEntry { Name = name, Keywords = new List<string>(keywords), Color = color };

        [TestMethod]
        public void MatchesExactCode_MECH()
        {
            var mapper = BuildMapper();
            var result = mapper.GetColor("7288_CON_MECH_D_FERG_UPPER-L02.nwc");
            Assert.IsNotNull(result);
            Assert.AreEqual((byte)0,   result.Value.R);
            Assert.AreEqual((byte)255, result.Value.G);
            Assert.AreEqual((byte)127, result.Value.B);
        }

        [TestMethod]
        public void MatchesAlternateCode_MD()
        {
            var mapper = BuildMapper();
            var result = mapper.GetColor("7288_CON_MD_FERG_UPPER-L02.nwc");
            Assert.IsNotNull(result);
            Assert.AreEqual((byte)0, result.Value.R);
        }

        [TestMethod]
        public void MatchesFP()
        {
            var mapper = BuildMapper();
            var result = mapper.GetColor("7288_CON_FP_ABL_US-L1.nwc");
            Assert.IsNotNull(result);
            Assert.AreEqual((byte)255, result.Value.R);
            Assert.AreEqual((byte)0,   result.Value.G);
        }

        [TestMethod]
        public void ReturnsNull_WhenNoMatch()
        {
            var mapper = BuildMapper();
            var result = mapper.GetColor("Upper_DES_ARCH.nwc");
            Assert.IsNull(result);
        }

        [TestMethod]
        public void ReturnsNull_ForEmptyFileName()
        {
            var mapper = BuildMapper();
            var result = mapper.GetColor("");
            Assert.IsNull(result);
        }

        [TestMethod]
        public void CaseInsensitiveMatch()
        {
            var mapper = BuildMapper();
            var result = mapper.GetColor("7288_CON_mech_FERG.nwc");
            Assert.IsNotNull(result);
        }

        // --- System color tests ---

        [TestMethod]
        public void GetSystemColor_MatchesPrimaryKeyword()
        {
            var config = new ColorConfig
            {
                Disciplines = new List<DisciplineEntry>(),
                Systems = new List<SystemEntry>
                {
                    MakeSystem("Supply Air", new[] { 0, 127, 255 }, "Supply Air", "SA")
                }
            };
            var mapper = new DisciplineColorMapper(config);
            var result = mapper.GetSystemColor("Supply Air");
            Assert.IsNotNull(result);
            Assert.AreEqual((byte)0,   result.Value.R);
            Assert.AreEqual((byte)127, result.Value.G);
            Assert.AreEqual((byte)255, result.Value.B);
        }

        [TestMethod]
        public void GetSystemColor_MatchesAliasKeyword()
        {
            var config = new ColorConfig
            {
                Disciplines = new List<DisciplineEntry>(),
                Systems = new List<SystemEntry>
                {
                    MakeSystem("Supply Air", new[] { 0, 127, 255 }, "Supply Air", "SA", "HVAC-SA")
                }
            };
            var mapper = new DisciplineColorMapper(config);
            // Different projects may use short codes or alternate names
            Assert.IsNotNull(mapper.GetSystemColor("SA"));
            Assert.IsNotNull(mapper.GetSystemColor("HVAC-SA"));
            Assert.IsNotNull(mapper.GetSystemColor("Ductwork: Supply Air (SA) +4 w.g"));
        }

        [TestMethod]
        public void GetSystemColor_CaseInsensitive()
        {
            var config = new ColorConfig
            {
                Disciplines = new List<DisciplineEntry>(),
                Systems = new List<SystemEntry>
                {
                    MakeSystem("Supply Air", new[] { 0, 127, 255 }, "Supply Air", "SA")
                }
            };
            var mapper = new DisciplineColorMapper(config);
            Assert.IsNotNull(mapper.GetSystemColor("supply air"));
            Assert.IsNotNull(mapper.GetSystemColor("SUPPLY AIR"));
        }

        [TestMethod]
        public void GetSystemColor_ReturnsNull_WhenNoMatch()
        {
            var config = new ColorConfig
            {
                Disciplines = new List<DisciplineEntry>(),
                Systems = new List<SystemEntry>
                {
                    MakeSystem("Supply Air", new[] { 0, 127, 255 }, "Supply Air", "SA")
                }
            };
            var mapper = new DisciplineColorMapper(config);
            Assert.IsNull(mapper.GetSystemColor("Return Air"));
        }

        [TestMethod]
        public void GetSystemColor_FallsBackToName_WhenKeywordsEmpty()
        {
            // Backward compat: old colors.json entries with no keywords array use Name as keyword
            var config = new ColorConfig
            {
                Disciplines = new List<DisciplineEntry>(),
                Systems = new List<SystemEntry>
                {
                    new SystemEntry { Name = "Supply Air", Keywords = new List<string>(), Color = new[] { 0, 127, 255 } }
                }
            };
            var mapper = new DisciplineColorMapper(config);
            Assert.IsNotNull(mapper.GetSystemColor("Supply Air"));
        }

        [TestMethod]
        public void GetSystemColor_FirstEntryWins_WhenMultipleMatch()
        {
            // More specific entries must be listed before less specific ones in colors.json.
            // "Sanitary Vent" before "Sanitary" ensures "Sanitary Vent" wins for vent lines.
            var config = new ColorConfig
            {
                Disciplines = new List<DisciplineEntry>(),
                Systems = new List<SystemEntry>
                {
                    MakeSystem("Sanitary Vent", new[] { 255, 191, 0 }, "Sanitary Vent", "SV"),
                    MakeSystem("Sanitary",       new[] { 255, 127, 0 }, "Sanitary", "Waste")
                }
            };
            var mapper = new DisciplineColorMapper(config);
            var result = mapper.GetSystemColor("Sanitary Vent Line");
            Assert.IsNotNull(result);
            Assert.AreEqual((byte)191, result.Value.G); // Sanitary Vent color, not Sanitary
        }

        // --- Word-boundary tests: short keywords must not match inside longer words ---

        [TestMethod]
        public void GetSystemColor_ShortKeyword_DoesNotMatchInsideWord()
        {
            // Bug: "RA" matches inside "Structural" via substring, coloring foundation slabs
            // and structural columns Return-Air green. Match must require word boundaries.
            var config = new ColorConfig
            {
                Disciplines = new List<DisciplineEntry>(),
                Systems = new List<SystemEntry>
                {
                    MakeSystem("Return Air", new[] { 0, 255, 127 }, "Return Air", "HVAC-RA", "RA")
                }
            };
            var mapper = new DisciplineColorMapper(config);
            Assert.IsNull(mapper.GetSystemColor("Structural Foundations"),
                "RA must not match inside 'structuRAl'");
            Assert.IsNull(mapper.GetSystemColor("Structural Columns"),
                "RA must not match inside 'structuRAl'");
            Assert.IsNull(mapper.GetSystemColor("Structural Framing"),
                "RA must not match inside 'structuRAl' or 'fRAming'");
        }

        [TestMethod]
        public void GetSystemColor_ShortKeyword_StillMatchesAtBoundaries()
        {
            var config = new ColorConfig
            {
                Disciplines = new List<DisciplineEntry>(),
                Systems = new List<SystemEntry>
                {
                    MakeSystem("Return Air", new[] { 0, 255, 127 }, "Return Air", "HVAC-RA", "RA")
                }
            };
            var mapper = new DisciplineColorMapper(config);
            Assert.IsNotNull(mapper.GetSystemColor("RA"));
            Assert.IsNotNull(mapper.GetSystemColor("RA-1"));
            Assert.IsNotNull(mapper.GetSystemColor("HVAC-RA"));
            Assert.IsNotNull(mapper.GetSystemColor("Ductwork: Return Air (RA) +0 w.g"));
        }

        [TestMethod]
        public void GetSystemColor_KeywordSeparator_TreatsSpaceHyphenUnderscoreAsInterchangeable()
        {
            // Bug: fab catalogs use hyphenated names like "Drainage: 01-Storm-Drain",
            // but keyword "Storm Drain" (with a literal space) would not match.
            // Treat space/hyphen/underscore as the same separator so authors can write
            // keywords readably and still match either form.
            var config = new ColorConfig
            {
                Disciplines = new List<DisciplineEntry>(),
                Systems = new List<SystemEntry>
                {
                    MakeSystem("Storm Drain", new[] { 128, 0, 255 }, "Storm Drain", "Roof Drain"),
                    MakeSystem("Domestic Cold Water", new[] { 0, 65, 255 }, "Cold Water", "DCW", "CW")
                }
            };
            var mapper = new DisciplineColorMapper(config);

            // The actual fab service value the user reported
            var stormResult = mapper.GetSystemColor("Drainage: 01-Storm-Drain");
            Assert.IsNotNull(stormResult, "Storm Drain keyword must match hyphenated form");
            Assert.AreEqual((byte)128, stormResult.Value.R);

            // Hyphen and underscore variants
            Assert.IsNotNull(mapper.GetSystemColor("Storm-Drain"));
            Assert.IsNotNull(mapper.GetSystemColor("Storm_Drain"));
            Assert.IsNotNull(mapper.GetSystemColor("Roof-Drain"));

            // Multi-word DCW keyword: "Cold Water" must match "Cold-Water"
            Assert.IsNotNull(mapper.GetSystemColor("Pipe: Cold-Water Service"));
        }

        [TestMethod]
        public void GetSystemMatch_ReturnsTransparency_WhenConfigured()
        {
            var config = new ColorConfig
            {
                Disciplines = new List<DisciplineEntry>(),
                Systems = new List<SystemEntry>
                {
                    new SystemEntry
                    {
                        Name = "Mechanical Equipment",
                        Keywords = new List<string> { "Mechanical Equipment" },
                        Color = new[] { 0, 0, 160 },
                        Transparency = 0.5
                    }
                }
            };
            var mapper = new DisciplineColorMapper(config);
            var match = mapper.GetSystemMatch("Mechanical Equipment");
            Assert.IsNotNull(match);
            Assert.AreEqual((byte)160, match.Color.B);
            Assert.AreEqual(0.5, match.Transparency, 0.0001);
        }

        [TestMethod]
        public void GetSystemMatch_TransparencyDefaultsToZero()
        {
            var config = new ColorConfig
            {
                Disciplines = new List<DisciplineEntry>(),
                Systems = new List<SystemEntry>
                {
                    MakeSystem("Supply Air", new[] { 0, 127, 255 }, "Supply Air", "SA")
                }
            };
            var mapper = new DisciplineColorMapper(config);
            var match = mapper.GetSystemMatch("Supply Air");
            Assert.IsNotNull(match);
            Assert.AreEqual(0.0, match.Transparency, 0.0001);
        }

        [TestMethod]
        public void GetSystemColor_KeywordWithRegexChars_IsTreatedLiterally()
        {
            // Keywords like "P - Sanitary Waste", "01-Refrigeration", "Fire Protection - Dry"
            // contain hyphens; they must match literally, not as regex syntax.
            var config = new ColorConfig
            {
                Disciplines = new List<DisciplineEntry>(),
                Systems = new List<SystemEntry>
                {
                    MakeSystem("Refrigerant", new[] { 0, 255, 255 }, "01-Refrigeration", "01-Refrigeration-Suction")
                }
            };
            var mapper = new DisciplineColorMapper(config);
            Assert.IsNotNull(mapper.GetSystemColor("01-Refrigeration"));
            Assert.IsNotNull(mapper.GetSystemColor("Pipe: 01-Refrigeration-Suction Line"));
        }
    }

    [TestClass]
    public class SpatialGridTests
    {
        [TestMethod]
        public void FindNearest_ReturnsNull_WhenEmpty()
        {
            var grid = new SpatialGrid(5.0);
            Assert.IsNull(grid.FindNearest(0, 0, 0));
        }

        [TestMethod]
        public void FindNearest_ReturnsSingleAnchor()
        {
            var grid = new SpatialGrid(5.0);
            var color = new RgbColor(255, 0, 0);
            grid.AddAnchor(1.0, 2.0, 3.0, color);
            var result = grid.FindNearest(1.0, 2.0, 3.0);
            Assert.IsNotNull(result);
            Assert.AreEqual((byte)255, result.Value.R);
            Assert.AreEqual((byte)0,   result.Value.G);
            Assert.AreEqual((byte)0,   result.Value.B);
        }

        [TestMethod]
        public void FindNearest_ReturnsClosestOfTwo()
        {
            var grid = new SpatialGrid(5.0);
            var red   = new RgbColor(255, 0, 0);
            var blue  = new RgbColor(0, 0, 255);
            grid.AddAnchor(0.0, 0.0, 0.0, red);
            grid.AddAnchor(10.0, 0.0, 0.0, blue);
            var result = grid.FindNearest(1.0, 0.0, 0.0);
            Assert.IsNotNull(result);
            Assert.AreEqual((byte)255, result.Value.R);
            Assert.AreEqual((byte)0,   result.Value.B);
        }

        [TestMethod]
        public void FindNearest_WorksAcrossCellBoundary()
        {
            var grid = new SpatialGrid(5.0);
            var color = new RgbColor(0, 255, 0);
            // Anchor is at x=4.9 (cell 0), query is at x=5.1 (cell 1) — adjacent cells
            grid.AddAnchor(4.9, 0.0, 0.0, color);
            var result = grid.FindNearest(5.1, 0.0, 0.0);
            Assert.IsNotNull(result);
            Assert.AreEqual((byte)255, result.Value.G);
        }

        [TestMethod]
        public void FindNearest_ReturnsNull_WhenNearestIsTooFar()
        {
            // Anchor at x=100 is in cell 20; query at origin is in cell 0.
            // Search covers cells -1..+1, so max reach is ~2 cell widths (~10 ft) per axis.
            // Cell 20 is well outside that range — FindNearest must return null.
            var grid = new SpatialGrid(5.0);
            grid.AddAnchor(100.0, 0.0, 0.0, new RgbColor(255, 0, 0));
            var result = grid.FindNearest(0.0, 0.0, 0.0);
            Assert.IsNull(result);
        }
    }
}
