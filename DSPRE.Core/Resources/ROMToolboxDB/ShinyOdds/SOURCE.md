# Shiny odds patch source

Eclipse's shiny odds patch for US HeartGold and SoulSilver, kept so the patch can be rebuilt. DSPRE doesn't run armips.
DSPRE embeds `../ShinyOdds.bin`, the assembled source, and `ShinyOddsPatch.cs` reads it.

| File | What it is |
|---|---|
| `ShinyChance_HG_SS.asm` | The armips source as Eclipse posted it, with ShinyOdds 4096. |
| `build_shiny_odds_bin.py` | Builds `ShinyOdds.bin` from three armips runs of the source. |

DSPRE writes the chosen odds into the ExtraShinyOdds word.

Source: Eclipse's post in the DS Pokémon hacking Discord, used with credit as the post asks.

SHA-256 of the source as received:

```
26b1174fe5da5f7938d570d6f15dd54b32d2db6fde3273708c91c2c0b5a5b2ba  ShinyChance_HG_SS.asm
```
