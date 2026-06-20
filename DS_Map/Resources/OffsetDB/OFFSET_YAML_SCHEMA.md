# Offset Database YAML Format

This document describes the correct format for offset database YAML files used by DSPRE.

## File Structure

Offset YAML files must have the following top-level structure:

```yaml
anchors:
  AnchorName1:
	# anchor definition
  AnchorName2:
	# anchor definition
```

## Anchor Definition

Each anchor must include all required properties:

```yaml
anchors:
  ItemTableOffset:
	source:
	  type: arm9                    # Required: arm9, overlay, or syntheticOverlay
	  overlayNumber: null           # Only required if type is 'overlay'
	length: 4                       # Number of bytes to read
	offsets:
	  English: 0xF85B4              # Language -> Hex offset mapping
	  Japanese: 0xF85C0
	  French: 0xF85C4
```

## Property Reference

### `source` (Required)
Defines where the data is located in the ROM.

#### `source.type` (Required String)
One of:
- `arm9` - Data is located in the ARM9 binary
- `overlay` - Data is located in an overlay file
- `syntheticOverlay` - Data is located in a synthetic/expanded overlay

#### `source.overlayNumber` (Optional Integer)
Required only if `type` is `overlay`. Specifies which overlay file to read from.

**Example with overlay:**
```yaml
  OverlayAnchor:
	source:
	  type: overlay
	  overlayNumber: 5
	length: 4
	offsets:
	  English: 0x1234
```

### `length` (Required Integer)
The number of bytes to read from the ROM for this anchor. Usually 4 for pointers/offsets.

### `offsets` (Required Dictionary)
Maps game languages to their corresponding hex offsets in the source location.

Supported language keys:
- `English`
- `Japanese`
- `French`
- `German`
- `Italian`
- `Spanish`
- `Korean`

You can also use version-specific overrides instead of direct values:

```yaml
  MultiVersionAnchor:
	source:
	  type: arm9
	length: 4
	offsets:
	  English:
		Diamond: 0xF85B4
		Pearl: 0xF85B8
		Platinum: 0xF85BC
	  Japanese:
		Diamond: 0xF85C0
		Pearl: 0xF85C4
		Platinum: 0xF85C8
```

### `decompRef` (Optional)
Reference to a decompiled symbol for this anchor.

```yaml
  SpecialAnchor:
	source:
	  type: arm9
	length: 4
	offsets:
	  English: 0xF85B4
	decompRef:
	  symbol: ItemTableStart
	  offset: 0x10
```

## Common Mistakes

### ❌ MISTAKE: `type` at wrong level
```yaml
ItemTableOffset:
  type: arm9          # WRONG - type should be under 'source'
  length: 4
  offsets:
	English: 0xF85B4
```

**Fix:**
```yaml
ItemTableOffset:
  source:
	type: arm9        # CORRECT
  length: 4
  offsets:
	English: 0xF85B4
```

---

### ❌ MISTAKE: Missing `source`
```yaml
ItemTableOffset:
  length: 4
  offsets:
	English: 0xF85B4
```

**Fix:** Add the `source` section with `type`.

---

### ❌ MISTAKE: Empty or missing `offsets`
```yaml
ItemTableOffset:
  source:
	type: arm9
  length: 4
  offsets:              # Missing language entries
```

**Fix:** Include at least one language offset:
```yaml
ItemTableOffset:
  source:
	type: arm9
  length: 4
  offsets:
	English: 0xF85B4
```

---

### ❌ MISTAKE: Indentation errors
```yaml
ItemTableOffset:
source:               # WRONG - not indented under ItemTableOffset
  type: arm9
length: 4
offsets:
  English: 0xF85B4
```

**Fix:** Use consistent 2-space indentation:
```yaml
ItemTableOffset:
  source:
	type: arm9
  length: 4
  offsets:
	English: 0xF85B4
```

---

### ❌ MISTAKE: Using tabs instead of spaces
YAML does not allow tabs for indentation. Always use spaces (2 or 4 per level).

---

### ❌ MISTAKE: Invalid hex values
```yaml
offsets:
  English: F85B4     # WRONG - missing 0x prefix
```

**Fix:** Use proper hex notation:
```yaml
offsets:
  English: 0xF85B4   # CORRECT
```

---

## Complete Example

```yaml
# Offset Database for Diamond/Pearl/Platinum
anchors:
  ItemTableOffset:
	source:
	  type: arm9
	length: 4
	offsets:
	  English: 0xF85B4
	  Japanese: 0xF85C0
	  French: 0xF85C4

  PokedexOffset:
	source:
	  type: arm9
	length: 4
	offsets:
	  English: 0x123456
	  Japanese: 0x123460

  SpecialOverlay:
	source:
	  type: overlay
	  overlayNumber: 5
	length: 4
	offsets:
	  English: 0x1000
	  Japanese: 0x1004
```

## Validation

The DSPRE will validate:
- ✓ All required properties are present
- ✓ `source.type` is one of the allowed values
- ✓ Hex offsets are valid
- ✓ At least one language offset is defined
- ✓ YAML syntax is correct

If validation fails, an error message with suggestions will be displayed.
