# Button & Timber

A small toy-shop sim set on Alder Lane. Make toys in the back room, find out what shoppers came for, and put your earnings toward the shop deed. The campaign runs for four six-day seasons. Save $1,750 by the end of winter to earn the deed, or keep trading in free play.

[Play in your browser](https://niposch.de/button-and-timber/)

## Play

Import `project.godot` in **Godot 4.7.2 .NET** with the **.NET SDK** installed. Press **F5** to build and run the game.

- **Shop** · Place the register and a display, then assign a toy. Displays refill from reserve. Match shoppers' symbols to your display tags. In the layout's **Decor** picker, buy a decoration or make it at the workshop. Each style brings in visitors. Seasonal pieces boost their toy family's demand during the matching season.
- **Street** · Morning light marks setup, daylight marks trading, and streetlamps come on toward closing. Pedestrians follow the speed controls, walk through the shop door when they visit, and return to the sidewalk on their way out.
- **Workshop** · Type up to 250 for a batch, or use **− / +**. Follow the tool bubble with **WASD**, the arrows, a controller D-pad, or the on-screen pad. Each machine handles a full pass before the cart carries the batch to its next station. **Set aside** saves unfinished work and **Resume** continues it using the same materials.
- **Blueprints** · 22 toys span four tiers. Each blueprint has its own levels, costs, and yield. Follow the Pip Engine through the Signal Cabin and Train Yard to reach the Midnight Express.
- **Stockroom** · The top bar shows used and total space. Open it to rent more space or select a toy and discard a quantity from reserve. Batches waiting for space resume once there is room. Bigger stockrooms raise the rent charged at closing.
- **Orders** · Fill seasonal and showcase requests or upgrade cart capacity.
- **Standing** · Earn renown to climb from Stallholder to Royal toymaker, which opens higher-tier patterns, staff, and the largest stockroom. From Craftsman, choose a shop style (Heirloom, Volume, or Showman). Hire an apprentice, window dresser, or cashier. Happy shoppers become regulars who return, bring friends' budgets, and tip.
- **Shoppers and prices** · Parents, bargain hunters, tourists, gift buyers, and collectors each haggle differently. Cheap shelves draw crowds, greedy ones cost reputation.
- **Days** · Random events such as school trips, rival shops, supplier sales, collector visits, and rain shape each morning. Every year sets a bigger profit target than the last.
- **Till report** · Check sales, expenses, and unmet requests. The doors close at 6 PM after the last shopper finishes. The speed controls advance the shop and unattended bench. Tool prompts keep their real-time duration while you work them.

Press **1**, **2**, and **3** to switch rooms. Press **Space** for the current shop action, such as opening the shop, ringing up a ready customer, or starting the next day. The daybook autosaves during play and when you close the game. **Options** has a manual save and a confirmed restart. Existing daybooks resume with their furnishings and progress. Use **New campaign** to try building from an empty shop.

## Art and sound

- The furniture, tools, workshop stations, carts, and seasonal decorations were made in Blender. Run `blender --background --python Art/Source/rotations.py` to bake the quarter-turn views of every placeable piece. Edit `Art/Source/Furniture.blend`, `Art/Source/Workshop.blend`, or `Art/Source/Fixtures.blend` to change their meshes and render new sprites.
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

## Browser builds

Run `pwsh -File build-web.ps1 -Setup` on Windows to install the pinned [Godot .NET web exporter](https://github.com/ComplexRobot/godot-dotnet-web-export/releases/tag/4.7.2), .NET SDK, and WebAssembly workload, and export to `builds/web`. Omit `-Setup` for subsequent builds. To test locally, serve the export with `python -m http.server --bind 127.0.0.1 --directory builds/web 8080` and open `http://127.0.0.1:8080/`. Append `?check=1` to run the in-browser smoke test and read its result in the browser console.

Pushes to `main` build and deploy the browser version with GitHub Actions. The Pages site inherits its domain from `niposch.de`. A service worker scoped to the game supplies the cross-origin isolation required by the threaded exporter.

## License

Original code and assets are licensed under [MIT](LICENSE). Third-party assets keep their original licenses. See [third-party notices](THIRD_PARTY.md) for attribution and runtime notices.
