# Trainer class metadata patch source

These are the original files of the trainer class metadata patch v1.0.0 by MrHam88, kept so the patch can be
rebuilt, changed, or moved to real assembly patching later. They are not compiled into DSPRE.

| File | What it is |
|---|---|
| `ham_hgss_trainer-class-metadata-expansion_v1.0.0.asm` | The armips patch source for US HeartGold. |
| `build_trainer_class_metadata_narc.py` | The one-shot builder that makes a/1/5/5 from the game's own tables. |
| `README.md` | The author's release and usage notes. |

DSPRE does not run armips. `../TrainerClassMetadataPatch.bin`, which DSPRE embeds, holds this source assembled once:
the original and patched bytes of every native write, the payload, and the places that refer to the payload's
address, so the toolbox can place the payload anywhere in the synthetic overlay. `TrainerClassMetadataPatch.cs`
reads it, and its `BuildRecords` is a port of the builder script.

Source: the release package attached to DSPRE pull request #272, used with the author's permission. The release's
recorded presentation media are not kept here.

Licence: MIT, as the README states. The release package names a `LICENSE` file that was not in the attachment.

SHA-256 of the files as received:

```
698eed63d31884d595fcc1cf72d672f9e51a13b3d531c4fad4b9566eb932eb41  ham_hgss_trainer-class-metadata-expansion_v1.0.0.asm
546ae03388d0e2b0ef5b9a3c197080c004476b7431e219aa080879ca7d033819  build_trainer_class_metadata_narc.py
f771052480de760aa300c06105d0b5331eaa5da7049e1524e42eef5d04de4cb3  README.md
```
