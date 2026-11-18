# Button & Timber

A small toy-shop sim set on Alder Lane. Make toys in the back room, find out what shoppers came for, and put your earnings toward the shop deed. The campaign runs for four six-day seasons. Save $1,750 by the end of winter to earn the deed, or keep trading in free play.

## Play

Import `project.godot` in **Godot 4.7.2 .NET** with the **.NET SDK** installed. Press **F5** to build and run the game.

- **Shop** · Place the register and a shelf or table from your crate, then assign a toy. Displays refill from reserve automatically. Shoppers browse and carry their finds to the register. Click it to ring up the person at the front of the line, or let the cashier handle it.
- **Workshop** · Compare patterns in one list, then queue a batch. Click the prompted tool or press **A** for saw, **S** for mallet, **D** for brush, or **F** for needle. Correct strokes speed up crafting and improve yield. A wrong tool greatly lowers that toy's chance of success. Basic and mastered recipes can run unattended. Difficult recipes wait for you at the bench.
- **Orders** · Fill seasonal requests from reserve and put the proceeds toward new patterns or the adjoining room.
- **Till report** · Check sales, expenses, and unmet requests. The doors close at 6 PM after the last shopper finishes. The speed controls advance the shop and unattended bench. Tool prompts keep their real-time duration while you work them.

Press **1**, **2**, and **3** to switch rooms. The daybook autosaves during play and when you close the game. **Options** has a manual save and a confirmed restart. Existing daybooks resume with their furnishings and progress. Use **New campaign** to try building from an empty shop.

## Art and sound

- The shop furniture and tools were made in Blender. Edit `Art/Source/Furniture.blend` to change their meshes and render new sprites.
- The animated shoppers are adapted from [Quaternius' Ultimate Animated Character Pack](https://quaternius.com/packs/ultimatedanimatedcharacter), the plants from [KayKit Furniture Bits](https://kaylousberg.itch.io/furniture-bits), and the interface audio from [Kenney Interface Sounds](https://kenney.nl/assets/interface-sounds). These packs are CC0 1.0.
- Run Blender in background mode with `--python Art/Source/sprite_packs.py -- <character-zip> <furniture-zip>` to rebake the third-party sprites from the original downloads. The game ships with the PNGs ready to use.

## Verify

Run these commands from the project directory with the installed Godot .NET executable on your path.

```text
godot --headless --editor --path . --import
dotnet build
godot --headless --path . -- --smoke-test
```

The smoke test covers furnishing, crafting outcomes, pathfinding, checkout, save migration and round-tripping, and both campaign outcomes. To capture the four game views, run `godot --path . -- --capture=.godot/preview`.
