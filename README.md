# Button & Timber

A small toy-shop sim set on Alder Lane. Make toys in the back room, find out what shoppers came for, and put your earnings toward the shop deed. The campaign runs for four six-day seasons. Save $1,750 by the end of winter to earn the deed, or keep trading in free play.

## Play

Import `project.godot` in **Godot 4.7.2 .NET** with the **.NET SDK** installed. Press **F5** to build and run the game.

- **Shop** · Place the register and a display, then assign a toy. Displays refill from reserve. Match shoppers' symbols to your display tags. In the layout's **Decor** picker, buy a decoration or make it at the workshop. Each style brings in visitors. Seasonal pieces boost their toy family's demand during the matching season.
- **Workshop** · Type up to 250 for a batch, or use **− / +**. Follow the tool bubble with **WASD**, the arrows, a controller D-pad, or the on-screen pad. Each machine handles a full pass before the cart carries the batch to its next station. **Set aside** saves unfinished work and **Resume** continues it using the same materials.
- **Blueprints** · 22 toys span four tiers. Each blueprint has its own levels, costs, and yield. Follow the Pip Engine through the Signal Cabin and Train Yard to reach the Midnight Express.
- **Orders** · Fill seasonal and showcase requests, upgrade cart capacity, or rent a larger stockroom. A full stockroom pauses finished output while the workshop holds the batch. Bigger stockrooms raise the rent charged at closing.
- **Till report** · Check sales, expenses, and unmet requests. The doors close at 6 PM after the last shopper finishes. The speed controls advance the shop and unattended bench. Tool prompts keep their real-time duration while you work them.

Press **1**, **2**, and **3** to switch rooms. The daybook autosaves during play and when you close the game. **Options** has a manual save and a confirmed restart. Existing daybooks resume with their furnishings and progress. Use **New campaign** to try building from an empty shop.

## Art and sound

- The furniture, tools, workshop stations, carts, and seasonal decorations were made in Blender. Edit `Art/Source/Furniture.blend`, `Art/Source/Workshop.blend`, or `Art/Source/Fixtures.blend` to change their meshes and render new sprites.
- The animated shoppers are adapted from [Quaternius' Ultimate Animated Character Pack](https://quaternius.com/packs/ultimatedanimatedcharacter), the plants from [KayKit Furniture Bits](https://kaylousberg.itch.io/furniture-bits), and the interface audio from [Kenney Interface Sounds](https://kenney.nl/assets/interface-sounds). These packs are CC0 1.0.
- Run Blender in background mode with `--python Art/Source/sprite_packs.py -- <character-zip> <furniture-zip>` to rebake the third-party sprites from the original downloads. The game ships with the PNGs ready to use.

## Verify

Run these commands from the project directory with the installed Godot .NET executable on your path.

```text
godot --headless --editor --path . --import
dotnet build
godot --headless --path . -- --smoke-test
```

The smoke test covers the controls, staged batches, cart logistics, storage and rent, seasonal decorations, save migration, checkout, and both campaign outcomes. To capture the shop, workshop, moving cart, and decor picker, run `godot --path . -- --capture=.godot/preview`.
