[Research](../../ResearchNotes.md) / [Move Research](../MoveResearch.md) / Move Animation Particle Placement

# Where moves put their particles

Generated from the Platinum ROM by `ParticleAnchorCoverageTests`. Do not edit by hand.

Two independent choices decide where a move's particles appear and how they leave. The anchor is
the last value on the ADD_PARTICLE command and says what the emitter is tied to. The emission
shape sits in the particle data and says how the particles are thrown from it. To check the
preview puts particles where the game does, one move from each row has to be recorded.

- 501 scripts read, 485 particle archives read
- 6 different anchors, 10 different emission shapes

## Anchors

| anchor | moves using it | some that do |
|---:|---:|---|
| 0 | 16 | 59 Blizzard, 97 Agility, 114 Haze, 195 Perish Song, 201 Sandstorm, 230 Sweet Scent, 234 Morning Sun, 240 Rain Dance, 241 Sunny Day, 247 Shadow Ball |
| 3 | 130 | 13 Razor Wind, 14 Swords Dance, 19 Fly, 45 Growl, 46 Roar, 47 Sing, 51 Acid, 63 Hyper Beam, 72 Mega Drain, 75 Razor Leaf |
| 4 | 229 | 0 -, 1 Pound, 2 Karate Chop, 3 DoubleSlap, 4 Comet Punch, 5 Mega Punch, 6 Pay Day, 7 Fire Punch, 8 Ice Punch, 9 ThunderPunch |
| 17 | 164 | 6 Pay Day, 16 Gust, 18 Whirlwind, 26 Jump Kick, 28 Sand-Attack, 37 Thrash, 40 Poison Sting, 41 Twineedle, 43 Leer, 46 Roar |
| 19 | 7 | 54 Mist, 113 Light Screen, 115 Reflect, 215 Heal Bell, 219 Safeguard, 312 Aromatherapy, 381 Lucky Chant |
| 20 | 17 | 13 Razor Wind, 57 Surf, 59 Blizzard, 75 Razor Leaf, 129 Swift, 145 Bubble, 157 Rock Slide, 181 Powder Snow, 191 Spikes, 196 Icy Wind |

## Emission shapes

| shape | archives using it | one to record |
|---:|---:|---|
| 0 | 323 | 1 Pound |
| 1 | 160 | 217 Present |
| 2 | 191 | 217 Present |
| 3 | 55 | 217 Present |
| 4 | 14 | 290 Secret Power |
| 5 | 24 | 9 ThunderPunch |
| 6 | 73 | 217 Present |
| 7 | 9 | 217 Present |
| 8 | 6 | 55 Water Gun |
| 9 | 1 | 237 Hidden Power |
