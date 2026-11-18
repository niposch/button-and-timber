# Button & Timber

A small toy-shop sim set on Alder Lane. Make toys in the back room, find out what shoppers came for, and put your earnings toward the shop deed. The campaign runs for four six-day seasons. Save $1,750 by the end of winter to earn the deed, or keep trading in free play.

## Play

Import `project.godot` in **Godot 4.7.2 .NET** with the **.NET SDK** installed. Press **F5** to build and run the game.

- **Shop** · Click a display to change its toy, set a price, fill it from the back room, or move it while the doors are shut. Open the shop when you're ready. Click shoppers to see their budget and what they're looking for.
- **Workshop** · Pick a pattern and queue a batch. Finished toys go into reserve. Give the bench a hand with **Space**. Practice improves cost and yield and helps unlock other patterns.
- **Town** · Fill a seasonal order from reserve or buy the permit for a larger shop.
- **Till report** · Check sales, expenses, and unmet requests. The shop closes at 6 PM. Use the speed controls to pause or advance trading and workshop orders.

Press **1**, **2**, and **3** to switch rooms. Changes save automatically. **Options** controls sound and decorative motion and lets you start a new campaign.

The rooms, props, toys, characters, and sound effects are drawn or synthesized in code. The shop, workshop, and town share the same isometric drawing tools.

## Verify

Run these commands from the project directory with the installed Godot .NET executable on your path.

```text
godot --headless --editor --path . --import
dotnet build
godot --headless --path . -- --smoke-test
```

The smoke test covers pricing, stock transfers, production, patterns, town orders, expansion, save data, and both campaign outcomes. To capture the four game views, run `godot --path . -- --capture=.godot/preview`.
