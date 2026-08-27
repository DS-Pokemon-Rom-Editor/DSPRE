namespace DSPRE
{
    /// <summary>
    /// Resolves a "common"/global script number against the CommonScript ID reference tables for
    /// Platinum and HGSS (identical across languages for a given game). Below 2000, a script number is
    /// always local to the current event's own paired script file, which DSPRE already handles; this
    /// resolver is only meant to be consulted as a fallback once that local lookup fails. Diamond/Pearl
    /// has no known table and is intentionally left alone (always returns NotCommon).
    /// </summary>
    public static class CommonScriptId
    {
        public enum Kind { NotCommon, Resolved, Discrepancy }

        public sealed class Result
        {
            public Kind Kind;
            public int ScriptArchiveId;
            public int TextArchiveId;

            // 0-based, exactly as the table's own "ID - lowerBound" formula gives it. DSPRE's own script
            // numbering (ScriptCommandContainer.manualUserID, what the Script Editor shows as "Script 1",
            // "Script 2"...) is 1-based, so the first script in a resolved archive is LocalScriptId == 0
            // but ManualUserId == 1. Use ManualUserId for anything shown to the user or matched against
            // a real ScriptFile.
            public int LocalScriptId;
            public int ManualUserId => LocalScriptId + 1;

            public int RangeLower;
            public int RangeUpper;
            public int[] CandidateArchives;
        }

        private struct Bracket
        {
            public int Lower;   // inclusive
            public int Upper;   // exclusive
            public int ScriptArchive;
            public int TextArchive;
            public int[] DiscrepancyCandidates; // non-null only for a Discrepancy bracket
        }

        private static readonly Bracket[] PlatBrackets =
        {
            new Bracket { Lower = 10490, Upper = 65536, ScriptArchive = 499,  TextArchive = 541 },
            new Bracket { Lower = 10450, Upper = 10490, ScriptArchive = 500,  TextArchive = 16   },
            new Bracket { Lower = 10400, Upper = 10450, ScriptArchive = 400,  TextArchive = 203  },
            new Bracket { Lower = 10200, Upper = 10400, ScriptArchive = 407,  TextArchive = 379  },
            new Bracket { Lower = 10150, Upper = 10200, ScriptArchive = 1116, TextArchive = 621  },
            new Bracket { Lower = 10100, Upper = 10150, ScriptArchive = 1115, TextArchive = 622  },
            new Bracket { Lower = 10000, Upper = 10100, ScriptArchive = 409,  TextArchive = 381  },
            new Bracket { Lower = 9950,  Upper = 10000, ScriptArchive = 411,  TextArchive = 383  },
            new Bracket { Lower = 9900,  Upper = 9950,  ScriptArchive = 397,  TextArchive = 213  },
            new Bracket { Lower = 9800,  Upper = 9900,  ScriptArchive = 212,  TextArchive = 217  },
            new Bracket { Lower = 9700,  Upper = 9800,  ScriptArchive = 422,  TextArchive = 429  },
            new Bracket { Lower = 9600,  Upper = 9700,  ScriptArchive = 412,  TextArchive = 213  },
            new Bracket { Lower = 9500,  Upper = 9600,  ScriptArchive = 501,  TextArchive = 547  },
            new Bracket { Lower = 9400,  Upper = 9500,  ScriptArchive = 426,  TextArchive = 432  },
            new Bracket { Lower = 9300,  Upper = 9400,  ScriptArchive = 406,  TextArchive = 374  },
            new Bracket { Lower = 9200,  Upper = 9300,  ScriptArchive = 423,  TextArchive = 430  },
            new Bracket { Lower = 9100,  Upper = 9200,  ScriptArchive = 0,    TextArchive = 11   },
            new Bracket { Lower = 9000,  Upper = 9100,  ScriptArchive = 213,  TextArchive = 221  },
            new Bracket { Lower = 8970,  Upper = 9000,  ScriptArchive = 425,  TextArchive = 7    },
            new Bracket { Lower = 8950,  Upper = 8970,  ScriptArchive = 498,  TextArchive = 539  },
            new Bracket { Lower = 8900,  Upper = 8950,  ScriptArchive = 424,  TextArchive = 431  },
            new Bracket { Lower = 8800,  Upper = 8900,  ScriptArchive = 497,  TextArchive = 538  },
            new Bracket { Lower = 8000,  Upper = 8800,  ScriptArchive = 408,  TextArchive = 380  }, // Hidden Items
            new Bracket { Lower = 7000,  Upper = 8000,  ScriptArchive = 404,  TextArchive = 369  }, // Ground Items
            new Bracket { Lower = 5000,  Upper = 7000,  ScriptArchive = 1114, TextArchive = 213  }, // Double Battles
            new Bracket { Lower = 3000,  Upper = 5000,  ScriptArchive = 1114, TextArchive = 213  }, // Single Battles
            new Bracket { Lower = 2800,  Upper = 3000,  ScriptArchive = 413,  TextArchive = 397  },
            new Bracket { Lower = 2500,  Upper = 2800,  ScriptArchive = 1,    TextArchive = 17   },
            new Bracket { Lower = 2000,  Upper = 2500,  ScriptArchive = 211,  TextArchive = 213  }, // "Common" Scripts
        };

        private static readonly Bracket[] HgssBrackets =
        {
            new Bracket { Lower = 10490, Upper = 65536, ScriptArchive = 263, TextArchive = 433 },
            new Bracket { Lower = 10450, Upper = 10490, ScriptArchive = 264, TextArchive = 19  },
            new Bracket { Lower = 10440, Upper = 10450, ScriptArchive = 2,   TextArchive = 748 },
            new Bracket { Lower = 10400, Upper = 10440, ScriptArchive = 151, TextArchive = 246 },
            new Bracket { Lower = 10350, Upper = 10400, ScriptArchive = 952, TextArchive = 726 },
            new Bracket { Lower = 10300, Upper = 10350, ScriptArchive = 734, TextArchive = 444 },
            new Bracket { Lower = 10200, Upper = 10300, ScriptArchive = 144, TextArchive = 209 },
            new Bracket { Lower = 10150, Upper = 10200, ScriptArchive = 955, TextArchive = 732 },
            new Bracket { Lower = 10100, Upper = 10150, ScriptArchive = 954, TextArchive = 733 },
            new Bracket { Lower = 10000, Upper = 10100, ScriptArchive = 146, TextArchive = 211 },
            new Bracket { Lower = 9950,  Upper = 10000, ScriptArchive = 148, TextArchive = 666 },
            new Bracket { Lower = 9900,  Upper = 9950,  ScriptArchive = 136, TextArchive = 40  },
            new Bracket { Lower = 9850,  Upper = 9900,  ScriptArchive = 167, TextArchive = 312 },
            new Bracket { Lower = 9800,  Upper = 9850,  ScriptArchive = 166, TextArchive = 43  },
            new Bracket { Lower = 9700,  Upper = 9800,  ScriptArchive = 163, TextArchive = 266 },
            // Source table is self-contradictory here: a "9600 <= ID < 9500" row (lower > upper) plus two
            // wide, overlapping rows (9500-9700 and 9300-9600) claiming different archives for the same
            // IDs. Surface it instead of guessing which one is right.
            new Bracket { Lower = 9300,  Upper = 9700,  DiscrepancyCandidates = new[] { 149, 265, 143 } },
            new Bracket { Lower = 9200,  Upper = 9300,  ScriptArchive = 164, TextArchive = 267 },
            new Bracket { Lower = 9100,  Upper = 9200,  ScriptArchive = 0,   TextArchive = 14  },
            new Bracket { Lower = 9000,  Upper = 9100,  ScriptArchive = 4,   TextArchive = 748 },
            new Bracket { Lower = 8900,  Upper = 9000,  ScriptArchive = 165, TextArchive = 268 },
            new Bracket { Lower = 8800,  Upper = 8900,  ScriptArchive = 262, TextArchive = 427 },
            new Bracket { Lower = 8000,  Upper = 8800,  ScriptArchive = 145, TextArchive = 210 }, // Hidden Items
            new Bracket { Lower = 7000,  Upper = 8000,  ScriptArchive = 141, TextArchive = 199 }, // Ground Items
            new Bracket { Lower = 5000,  Upper = 7000,  ScriptArchive = 953, TextArchive = 40  }, // Double Battles
            new Bracket { Lower = 3000,  Upper = 5000,  ScriptArchive = 953, TextArchive = 40  }, // Single Battles
            new Bracket { Lower = 2800,  Upper = 3000,  ScriptArchive = 150, TextArchive = 23  },
            new Bracket { Lower = 2500,  Upper = 2800,  ScriptArchive = 1,   TextArchive = 20  },
            new Bracket { Lower = 2000,  Upper = 2500,  ScriptArchive = 3,   TextArchive = 40  }, // "Common" Scripts
        };

        /// <summary>Resolves a script number that fell outside the current event's own paired script
        /// file. Only meaningful for numbers &gt;= 2000 on Platinum/HGSS; everything else (including all
        /// of Diamond/Pearl, which has no known table) returns NotCommon so callers keep their existing
        /// local-only behavior.</summary>
        public static Result Resolve(RomInfo.GameFamilies family, int scriptNumber)
        {
            if (scriptNumber < 2000)
            {
                return new Result { Kind = Kind.NotCommon };
            }

            Bracket[] table;
            if (family == RomInfo.GameFamilies.Plat) table = PlatBrackets;
            else if (family == RomInfo.GameFamilies.HGSS) table = HgssBrackets;
            else return new Result { Kind = Kind.NotCommon }; // DP: no known table, old behavior stands

            foreach (var b in table)
            {
                if (scriptNumber < b.Lower || scriptNumber >= b.Upper)
                {
                    continue;
                }

                if (b.DiscrepancyCandidates != null)
                {
                    return new Result
                    {
                        Kind = Kind.Discrepancy,
                        RangeLower = b.Lower,
                        RangeUpper = b.Upper,
                        CandidateArchives = b.DiscrepancyCandidates
                    };
                }

                return new Result
                {
                    Kind = Kind.Resolved,
                    ScriptArchiveId = b.ScriptArchive,
                    TextArchiveId = b.TextArchive,
                    LocalScriptId = scriptNumber - b.Lower
                };
            }

            return new Result { Kind = Kind.NotCommon };
        }
    }
}
