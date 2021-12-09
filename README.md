# VRChat Axe Throwing Game

This is the axe throwing game board behind my axe throwing map in VRChat. It is compatible with PC and Quest.

There are multiple game modes and the board supports a generic style of game rules. Game modes can allow multiple players per board. Included are these game modes:

- Standard Board (World axe throwing league rules)
- Beer Axe 2 player board
- Beer Axe 1 player board

## Crediting

If you use this in your world, I would appreciate it if you add a credit to the world and keep any credit/branding that appears baked into the prefab.

## Throwing Physics details

The player's throw is smoothed using a linear regression. This allows better for the player intent to show through in the resulting throw than other techniques used for smoothing. An assist system guides the player's throw towards the goal velocity for a proper axe throw, with more powerful assistance the more accurate the player's throw is to the target velocity. This results in a great degree of consistency once a player has dialed in their throwing skills and makes higher level gameplay possible in VR Chat.

The axe spin is much more forgiving than the velocity and is scaled to match the speed the player actually throws at, targeting 1 revolution from a distance just behind the line. This significantly reduces the difficulty of sticking the axe. While playing without this assist turned up quite as heavily *is possible* it's not pleasant and does not feel realistic at all. In real life, the spin of an axe becomes proportional to the throw speed given proper technique. In VR there is no mechanical feedback from the mass and rotational inertia of the axe on your arm and wrist, so the spin is essentialy caused by the flicking action of your wrist alone. This was frustrating for new players and not really fun for experienced players. And, because the moment of inertia of different controllers is very different, the wrist flick amount inherent to a natural throw is platform dependant.

Even though the controllers are very different, the game plays similarly with both Index controllers and Oculus Quest 2 controllers. The main difference is that the wrist action is harder to control on quest because the controller extends beyond the player's hand.

Overall, the game is challenging at first. However, most players quickly develop skill in the first play session and continue to make progress as they play.

Desktop gameplay is not a focus of this game. Desktop players get an automatic throw speed with some random elements and the gameplay is very simple.
