# VRChat Axe Throwing Game v2.1.1

> :warning: **Version 2.x This prefab uses U# v1.0.0b12**: You can get this from Merlin's discord. The upgrade from previous versions of U# to U# 1.0 is automated but is NOT production ready as of the time of this release. You have been warned!

This is the axe throwing game board behind my axe throwing map in VRChat. It is compatible with PC and Quest.

There are multiple game modes and the board supports a generic style of game rules. Game modes can allow multiple players per board. Included are these game modes:

- Standard Board (World axe throwing league rules)
- Beer Axe 2 player board
- Beer Axe 1 player board

# Crediting

If you use this in your world, I would appreciate it if you add a credit to the world and keep any credit/branding that appears baked into the prefab.

# Choosing your version

Version 2 is a significant improvement over version 1. Use it if you can. However, if you cannot or would prefer not to upgrade your world to U# 1.0 yet, use version 1 of the release.

# v2.0 Axe Physics Simulation details

The throw physics are simulated by pretending the axe is a sphere until it reaches the board, at which point the intersection of the axe cutting edge and the board is determined iteratively. It turns out that the handle bounce is quite an important detail in real axe throwing physics. I took a very naive and forgiving approach to simulating the handle bounce for gameplay purposes. The result is extremely consistent and the axe doesn't randomly fail to stick if the player throws with consistency.

## Throw Smoothing

The player's throw is smoothed using a linear regression. This allows better for the player intent to show through in the resulting throw than other techniques used for smoothing. 

An assist system guides the player's throw towards the goal velocity for a proper axe throw, with more powerful assistance the more accurate the player's throw is to the target velocity. This results in a great degree of consistency once a player has dialed in their throwing skills and makes higher level gameplay possible in VR Chat.

Even though the controllers are very different, the game plays similarly with both Index controllers and Oculus Quest 2 controllers. The main difference is that the wrist action is harder to control on quest because the controller extends beyond the player's hand.

Overall, the game is challenging at first. However, most players quickly develop skill in the first play session and continue to make progress as they play. However, everyone's skill level is different. I've seen players easily reach 30+ points in their very first play session while others are incapable of sticking the axe more than once or twice a game.

Desktop gameplay is not a focus of this game. Desktop players get an automatic throw speed with some random elements and the gameplay is very simple.
