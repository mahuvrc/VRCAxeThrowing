# VRChat Axe Throwing Game v2.1.1

> **Version 2.x This prefab uses U# v1.0+**: You can get this from the VRC creator assistant.

This is the axe throwing game board behind my axe throwing map in VRChat. It is compatible with PC and Quest.

There are multiple game modes and the board supports a generic style of game rules. Game modes can allow multiple players per board. Included are these game modes:

- Standard Board (World axe throwing league rules)
- Beer Axe 2 player board
- Beer Axe 1 player board

# Usage

> Version 2 is a significant improvement over version 1. Use it if you can. However, if you cannot or would prefer not to upgrade your world to U# 1.0 yet, use version 1 of the release. If you're upgrading from version 1 to version 2 you will want to delete the old mahu/axe-throwing folder from your assets folder before upgrading.
>
> If you haven't already imported the latest VRChat SDK and either UdonSharp 1.0 beta for axe or 0.20.3 you'll need to import those first before importing the axe throwing package.

1. Download the unity package from the [releases section of this repository](https://github.com/mahuvrc/VRCAxeThrowing/releases).
2. Import the package
3. navigate to the mahu/axe-throwing folder in the project window
4. Drag and drop the `Axe Throwing Game.prefab` prefab into your scene
5. Don't move any of the components of the prefab into position individually. Instead, move the whole prefab into position as a single unit.

You can place multiple boards in your world. I have 12 in my axe throwing world and there are no performance issues on quest. Each board has an update script on the axe itself, though, so do not add an unnecessary number of games to your world.

## Crediting

If you use this in your world, I would appreciate it if you add a credit to the world and keep the credit text that exists on the prefab's UI.

# Customization Tips

* It's recommended to build some dividers between lanes when placing the pefab in your world. Real life axe throwing usually has a divider between every 2 lanes.
* Do not change the scale of the axe/board
* Do not change the distance from the line to the board.
* Do not place any geometry between the player and the board as this may interfere with physics simulation.
* The axe model can be *visually* customized/replaced, but do not change the colliders on the axe. Make sure the edge of the axe and the handle of the axe match the existing model's size well.
* The axe holder can be visually customized as well. Keep in mind that the handle object is hidden while it is locked.

## Score Callback

Included is an abstract class called ScoreCallback. The standard game mode uses this to report the player's score when the match is over. You can use this to integrate something like a points system or a high score board for your world. This is a very generic and simple interface and I intend to continue to support it in future versions or at least provide a clear upgrade path in future versions.

## Custom Game Modes

It's possible to implement your own custom game mode but these custom modes will probably break in future versions of the prefab. I plan to continue refactoring the game mode system.

# v2.0 Axe Physics Simulation details

The throw physics are simulated by pretending the axe is a sphere until it reaches the board, at which point the intersection of the axe cutting edge and the board is determined iteratively. It turns out that the handle bounce is quite an important detail in real axe throwing physics. I took a very naive and forgiving approach to simulating the handle bounce for gameplay purposes. The result is extremely consistent and the axe doesn't randomly fail to stick if the player throws with consistency.

## Throw Smoothing

The player's throw is smoothed using a linear regression. This allows better for the player intent to show through in the resulting throw than other techniques used for smoothing. 

An assist system guides the player's throw towards the goal velocity for a proper axe throw, with more powerful assistance the more accurate the player's throw is to the target velocity. This results in a great degree of consistency once a player has dialed in their throwing skills and makes higher level gameplay possible in VR Chat.

Even though the controllers are very different, the game plays similarly with both Index controllers and Oculus Quest 2 controllers. The main difference is that the wrist action is harder to control on quest because the controller extends beyond the player's hand.

Overall, the game is challenging at first. However, most players quickly develop skill in the first play session and continue to make progress as they play. However, everyone's skill level is different. I've seen players easily reach 30+ points in their very first play session while others are incapable of sticking the axe more than once or twice a game.

Desktop gameplay is not a focus of this game. Desktop players get an automatic throw speed with some random elements and the gameplay is very simple.
