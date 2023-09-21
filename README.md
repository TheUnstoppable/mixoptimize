# MixOptimize
Applies various optimizations to C&amp;C Renegade Mix and Pkg files to improve performance and reduce size.


## Usage
It is possible to use MixOptimize manually with command line, or automatically via an automated batch script by passing the file to standard input.

### Options
- `--skip-texture-conversion`: Skips texture conversion to DDS.
- `--skip-texture-resize`: Skips resizing textures to a square.
- `--max-exponent <value>`: The maximum power of two to use while resizing. (Default: 9 -> 2^9 = 512)
- `--skip-sounds`: Skips re-encoding sounds to MP3 @ 128 kbps.
- `--skip-confirmation`: Skips confirmation for the changes to be done.
- `--read-stdin`: Reads the file from standard input instead. (Implies `--skip-confirmation`)
- `--out`: Output file. (Required if `--read-stdin` is specified)

### Examples

#### Using MixOptimize from command line
`mixoptimize "C&C_Cool_Map.mix"`

#### Skipping sounds
`mixoptimize --skip-sounds "C&C_Cool_Map.mix"`

#### Skipping texture conversion and confirmations
`mixoptimize --skip-texture-conversion --skip-confirmation "C&C_Cool_Map.mix"`

#### Passing the map to standard input
`mixoptimize --read-stdin --out "C&C_Cooler_Map.mix" < "C&C_Cool_Map.mix"`


## Support
If you need help or want to suggest a feature/improvement, you will get the fastest response by joining in Discord server at https://discord.gg/KjeQ7xv, but be sure to read `#important` channel first.
