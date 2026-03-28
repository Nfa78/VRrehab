# Apartment Scene Workflow (Kitchen)

**Scope**: This is a code-based workflow map for Assets/Scenes/ApartmentScene.unity, derived from the scripts in Assets/Scripts. Use this to track missing references and runtime errors.

**Key Scripts in the Flow**
1. Assets/Scripts/LoadCharacter.cs
2. Assets/Scripts/PointsManager.cs
3. Assets/Scripts/KitchenTask01ScoreHandler.cs
4. Assets/Scripts/ScoreOpenDrawer.cs
5. Assets/Scripts/ScoreGrabThing.cs
6. Assets/Scripts/ScoreGrabAndPlaceThing.cs
7. Assets/Scripts/ArmExtensionDetector.cs
8. Assets/Scripts/HandDetection.cs
9. Assets/Scripts/PanOnStoveObjective.cs
10. Assets/Scripts/CarrotOnBoardObjective.cs
11. Assets/Scripts/CarrotsInPanObjective.cs
12. Assets/Scripts/OilOnCarrotsObjective.cs
13. Assets/Scripts/StoveFireHandler.cs
14. Assets/Scripts/StirringCarrotsObjective.cs
15. Assets/Scripts/ArrowHandler.cs

**Scene Startup Sequence (High Level)**
1. **PointsManager** initializes in Awake and persists via DontDestroyOnLoad.
2. **LoadCharacter** runs in Start:
   - Reads PlayerPrefs.selectedCharacter
   - Ensures PointsManager exists
   - Sets environment, helper NPC, and dialogue objects
   - Resets points based on current scene name
3. **KitchenTask01ScoreHandler** runs in Start:
   - Calls ScoreOpenDrawer.DoYourThing() to begin task chain

**Task Flow (Kitchen)**
1. **Open Drawer Sequence** (ScoreOpenDrawer)
   - Starts arm extension timer via ArmExtensionDetector
   - On success, starts grab timer via HandDetection
   - Then starts arm contraction timer
   - Marks done = true
2. **Grab & Place Pan** (KitchenTask01ScoreHandler -> ScoreGrabAndPlaceThing)
   - Kicks off a ScoreGrabThing sequence for the pan
3. **Grab & Place Carrot** (KitchenTask01ScoreHandler -> ScoreGrabAndPlaceThing)
4. **Pan On Stove Objective** (PanOnStoveObjective)
   - When pan enters trigger: locks pan, updates UI, gives points, opens next tasks
5. **Slice Carrots** (CarrotOnBoardObjective)
   - When carrot slices hit trigger: updates UI, gives points, enables knife task
6. **Carrots In Pan** (CarrotsInPanObjective)
   - Counts carrot pieces entering pan, updates UI, awards points by time
7. **Pour Oil** (OilOnCarrotsObjective)
   - Tracks oil particle collisions and pan hit, updates UI
8. **Turn Stove On/Off** (StoveFireHandler)
   - Monitors knob rotation, starts fire effects, awards points
   - Spawns spoon grab sequence via ScoreGrabThing
9. **Stir Carrots** (StirringCarrotsObjective)
   - Tracks spoon inside pan for a duration, updates UI, awards points
10. **Win Condition** (StoveFireHandler)
   - After stirring + stove off, shows win canvas and saves points

**Required Inspector Wiring (Common Failure Points)**
1. LoadCharacter
   - characterPrefabs, dialogues, spawnPoint, checklist, writtenDialogue, superfluousObjects
2. KitchenTask01ScoreHandler
   - sod, sgapt
   - pan + panHandDetectionComponent + panTargetTransform
   - carrot + carrotHandDetectionComponent + carrotTargetTransform
3. ScoreOpenDrawer
   - rmExtensionDetector, handDetection, drawer, 	argetPosition
4. ScoreGrabThing
   - rmExtensionDetector
5. ScoreGrabAndPlaceThing
   - scoreGrabThing, succeededTag
6. ArmExtensionDetector
   - headTransform, leftHandTransform, ightHandTransform, 	imerTag
7. HandDetection
   - Requires HandGrabInteractable on same GameObject
   - 	imerTag, outline or knifeOutline if used
8. PanOnStoveObjective
   - 	rigger, checkbox, rrow, otherArrow
   - drawer, drawerClosedPosition
   - panHandGrabInteraction
9. CarrotOnBoardObjective
   - 	rigger, checkbox, rrow
   - knife, knifeOutline, knifeHandDetectionComponent, knifeTargetTransform
10. CarrotsInPanObjective
    - success audio, checkbox, oilArrow, 	ask03Text, 	ask04Text
11. OilOnCarrotsObjective
    - oil particle system, checkbox, stoveKnobArrow
12. StoveFireHandler
    - Fire particle systems + ireLight, success, checkbox, checkbox02
    - scoreGrabThing, spoon, spoonHDComponent
    - winCanvas, 	ask04Text to 	ask07Text
13. StirringCarrotsObjective
    - 	imerTag, checkbox, 	ask05Text, 	ask06Text
14. ArrowHandler
    - 	argetObject, rrow, otherArrow, optional text + outlines

**Typical Error Sources**
1. Null references when any of the above fields are not assigned in the Inspector
2. HandDetection not finding HandGrabInteractable on the same GameObject
3. PointsManager.Instance not available early enough or not persisted between scenes
4. Interaction conflicts if Unity XRI and Meta Interaction SDK are used on the same objects

**Debug Strategy**
1. Start with LoadCharacter and PointsManager in a clean scene
2. Verify KitchenTask01ScoreHandler starts and ScoreOpenDrawer progresses
3. Add objectives one by one and confirm each trigger fires and UI updates
4. Use Debug.Log at each transition to confirm order
