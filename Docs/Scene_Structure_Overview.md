# Scene Structure Overview

This document lists the main scripts per scene and a high?level connection flow based on attached MonoBehaviours.

## ApartmentScene.unity

Scene file: `Assets/Scenes/ApartmentScene.unity`

Highlights
- `Assets/Scripts/LoadCharacter.cs` (Scene Manager)
- `Assets/Scripts/CollisionManager.cs` (Scene Manager)
- `Assets/Scripts/AppManager.cs` (Scene Manager)
- `Assets/Scripts/MenuHandler.cs` (Scene Manager)
- `Assets/Scripts/ArmExtensionDetector.cs` (Scene Manager)
- `Assets/Scripts/ScoreOpenDrawer.cs` (Scene Manager)
- `Assets/Scripts/ScoreGrabThing.cs` (Scene Manager)
- `Assets/Scripts/ScoreGrabAndPlaceThing.cs` (Scene Manager)
- `Assets/Scripts/KitchenTask01ScoreHandler.cs` (Scene Manager)
- `Assets/Scripts/StarsHandler.cs` (Scene Manager)
- `Assets/Scripts/PanOnStoveObjective.cs` (StoveTriggerCollider)
- `Assets/Scripts/FaucetHandler.cs` (Faucet_01)
- `Assets/Scripts/OutlinePulse.cs` (GameObject(389386441))
- `Assets/Scripts/CarrotOnBoardObjective.cs` (BoardTriggerCollider)
- `Assets/Scripts/CharacterSelection.cs` (Back2MenuButton)
- `Assets/Scripts/ArrowHandler.cs` (ISDK_HandGrabInteraction)
- `Assets/Scripts/HandDetection.cs` (ISDK_HandGrabInteraction)
- `Assets/Scripts/SimpleHandDetection.cs` (ISDK_HandGrabInteraction)
- `Assets/Scripts/CanvasActivator.cs` (Socket_1)
- `Assets/Scripts/LocalizedAudioChanger.cs` (ManAudio)
- `Assets/Scripts/PlayLocalizedAudio.cs` (ManAudio)
- `Assets/Scripts/DebugInfoHandler.cs` (GameObject(746543271))
- `Assets/Scripts/StoveFireHandler.cs` (ISDK_HandGrabInteraction)
- `Assets/Scripts/CarrotsInPanObjective.cs` (InsidePanTrigger)
- `Assets/Scripts/StirringCarrotsObjective.cs` (InsidePanTrigger)
- `Assets/Scripts/GripColorChanger.cs` (Cube)

Scene Boot & State
- `Assets/Scripts/LoadCharacter.cs` (Scene Manager)
- `Assets/Scripts/CollisionManager.cs` (Scene Manager)
- `Assets/Scripts/AppManager.cs` (Scene Manager)
- `Assets/Scripts/MenuHandler.cs` (Scene Manager)
- `Assets/Scripts/ArmExtensionDetector.cs` (Scene Manager)
- `Assets/Scripts/StarsHandler.cs` (Scene Manager)

Interaction & Input
- `Assets/SnapInteractionAssets/Scripts/SnapInteractableVisuals.cs` (StoveTriggerCollider)
- `Assets/Scripts/HandDetection.cs` (ISDK_HandGrabInteraction)
- `Assets/Scripts/SimpleHandDetection.cs` (ISDK_HandGrabInteraction)
- `Assets/Scripts/GripColorChanger.cs` (Cube)

Task & Objective Scripts
- `Assets/Scripts/MenuHandler.cs` (Scene Manager)
- `Assets/Scripts/ScoreOpenDrawer.cs` (Scene Manager)
- `Assets/Scripts/ScoreGrabThing.cs` (Scene Manager)
- `Assets/Scripts/ScoreGrabAndPlaceThing.cs` (Scene Manager)
- `Assets/Scripts/KitchenTask01ScoreHandler.cs` (Scene Manager)
- `Assets/Scripts/StarsHandler.cs` (Scene Manager)
- `Assets/Scripts/PanOnStoveObjective.cs` (StoveTriggerCollider)
- `Assets/Scripts/FaucetHandler.cs` (Faucet_01)
- `Assets/Scripts/CarrotOnBoardObjective.cs` (BoardTriggerCollider)
- `Assets/Scripts/ArrowHandler.cs` (ISDK_HandGrabInteraction)
- `Assets/Scripts/DebugInfoHandler.cs` (GameObject(746543271))
- `Assets/Scripts/StoveFireHandler.cs` (ISDK_HandGrabInteraction)
- `Assets/Scripts/CarrotsInPanObjective.cs` (InsidePanTrigger)
- `Assets/Scripts/StirringCarrotsObjective.cs` (InsidePanTrigger)

Guidance & UI
- `Assets/Scripts/OutlinePulse.cs` (GameObject(389386441))
- `Assets/Scripts/ArrowHandler.cs` (ISDK_HandGrabInteraction)
- `Assets/Scripts/CanvasActivator.cs` (Socket_1)

Dialogue & Localization
- `Assets/Scripts/LocalizedAudioChanger.cs` (ManAudio)
- `Assets/Scripts/PlayLocalizedAudio.cs` (ManAudio)

Menu / Selection
- `Assets/Scripts/CharacterSelection.cs` (Back2MenuButton)

Flow (high level)
```text
[Scene Load]
-> Scene boot (LoadCharacter/AppManager/MenuHandler/Points/Stars)
-> Task chain (KitchenTask01ScoreHandler -> ScoreOpenDrawer -> ScoreGrabThing -> ScoreGrabAndPlaceThing)
-> Objectives (PanOnStoveObjective -> CarrotOnBoardObjective -> CarrotsInPanObjective -> OilOnCarrotsObjective -> StoveFireHandler -> StirringCarrotsObjective)
-> Guidance/UI (ArrowHandler/OutlinePulse/Canvas*)
-> Dialogue/Audio (LocalizedAudioChanger/PlayLocalizedAudio)
-> Menu/Selection (CharacterSelection/UsernameSelection/CustomDropdown)
```

## HandGrabTest.unity

Scene file: `Assets/Scenes/HandGrabTest.unity`

Highlights
- No custom scripts detected

Scene Boot & State
- None

Interaction & Input
- None

Task & Objective Scripts
- None

Guidance & UI
- None

Dialogue & Localization
- None

Menu / Selection
- None

Flow (high level)
```text
[Scene Load]
```

## LeftApartmentScene.unity

Scene file: `Assets/Scenes/LeftApartmentScene.unity`

Highlights
- `Assets/Scripts/LoadCharacter.cs` (Scene Manager)
- `Assets/Scripts/CollisionManager.cs` (Scene Manager)
- `Assets/Scripts/AppManager.cs` (Scene Manager)
- `Assets/Scripts/MenuHandler.cs` (Scene Manager)
- `Assets/Scripts/ArmExtensionDetector.cs` (Scene Manager)
- `Assets/Scripts/ScoreOpenDrawer.cs` (Scene Manager)
- `Assets/Scripts/ScoreGrabThing.cs` (Scene Manager)
- `Assets/Scripts/ScoreGrabAndPlaceThing.cs` (Scene Manager)
- `Assets/Scripts/KitchenTask01ScoreHandler.cs` (Scene Manager)
- `Assets/Scripts/StarsHandler.cs` (Scene Manager)
- `Assets/Scripts/PanOnStoveObjective.cs` (StoveTriggerCollider)
- `Assets/Scripts/FaucetHandler.cs` (Faucet_01)
- `Assets/Scripts/OutlinePulse.cs` (GameObject(389386441))
- `Assets/Scripts/CarrotOnBoardObjective.cs` (BoardTriggerCollider)
- `Assets/Scripts/ArrowHandler.cs` (ISDK_HandGrabInteraction)
- `Assets/Scripts/HandDetection.cs` (ISDK_HandGrabInteraction)
- `Assets/Scripts/CanvasActivator.cs` (Socket_1)
- `Assets/Scripts/LocalizedAudioChanger.cs` (ManAudio)
- `Assets/Scripts/PlayLocalizedAudio.cs` (ManAudio)
- `Assets/Scripts/StoveFireHandler.cs` (ISDK_HandGrabInteraction)
- `Assets/Scripts/CarrotsInPanObjective.cs` (InsidePanTrigger)
- `Assets/Scripts/StirringCarrotsObjective.cs` (InsidePanTrigger)
- `Assets/Scripts/OilOnCarrotsObjective.cs` (Oil)
- `Assets/Scripts/GripColorChanger.cs` (Cube)

Scene Boot & State
- `Assets/Scripts/LoadCharacter.cs` (Scene Manager)
- `Assets/Scripts/CollisionManager.cs` (Scene Manager)
- `Assets/Scripts/AppManager.cs` (Scene Manager)
- `Assets/Scripts/MenuHandler.cs` (Scene Manager)
- `Assets/Scripts/ArmExtensionDetector.cs` (Scene Manager)
- `Assets/Scripts/StarsHandler.cs` (Scene Manager)

Interaction & Input
- `Assets/SnapInteractionAssets/Scripts/SnapInteractableVisuals.cs` (PanAnchor)
- `Assets/Scripts/HandDetection.cs` (ISDK_HandGrabInteraction)
- `Assets/Scripts/GripColorChanger.cs` (Cube)

Task & Objective Scripts
- `Assets/Scripts/MenuHandler.cs` (Scene Manager)
- `Assets/Scripts/ScoreOpenDrawer.cs` (Scene Manager)
- `Assets/Scripts/ScoreGrabThing.cs` (Scene Manager)
- `Assets/Scripts/ScoreGrabAndPlaceThing.cs` (Scene Manager)
- `Assets/Scripts/KitchenTask01ScoreHandler.cs` (Scene Manager)
- `Assets/Scripts/StarsHandler.cs` (Scene Manager)
- `Assets/Scripts/PanOnStoveObjective.cs` (StoveTriggerCollider)
- `Assets/Scripts/FaucetHandler.cs` (Faucet_01)
- `Assets/Scripts/CarrotOnBoardObjective.cs` (BoardTriggerCollider)
- `Assets/Scripts/ArrowHandler.cs` (ISDK_HandGrabInteraction)
- `Assets/Scripts/StoveFireHandler.cs` (ISDK_HandGrabInteraction)
- `Assets/Scripts/CarrotsInPanObjective.cs` (InsidePanTrigger)
- `Assets/Scripts/StirringCarrotsObjective.cs` (InsidePanTrigger)
- `Assets/Scripts/OilOnCarrotsObjective.cs` (Oil)

Guidance & UI
- `Assets/Scripts/OutlinePulse.cs` (GameObject(389386441))
- `Assets/Scripts/ArrowHandler.cs` (ISDK_HandGrabInteraction)
- `Assets/Scripts/CanvasActivator.cs` (Socket_1)

Dialogue & Localization
- `Assets/Scripts/LocalizedAudioChanger.cs` (ManAudio)
- `Assets/Scripts/PlayLocalizedAudio.cs` (ManAudio)

Menu / Selection
- None

Flow (high level)
```text
[Scene Load]
-> Scene boot (LoadCharacter/AppManager/MenuHandler/Points/Stars)
-> Task chain (KitchenTask01ScoreHandler -> ScoreOpenDrawer -> ScoreGrabThing -> ScoreGrabAndPlaceThing)
-> Objectives (PanOnStoveObjective -> CarrotOnBoardObjective -> CarrotsInPanObjective -> OilOnCarrotsObjective -> StoveFireHandler -> StirringCarrotsObjective)
-> Guidance/UI (ArrowHandler/OutlinePulse/Canvas*)
-> Dialogue/Audio (LocalizedAudioChanger/PlayLocalizedAudio)
```

## NewStarterMenu.unity

Scene file: `Assets/Scenes/NewStarterMenu.unity`

Highlights
- `Assets/Scripts/StarsHandler.cs` (CoffeeButton)
- `Assets/Scripts/UsernameSelection.cs` (GameObject(869670615))
- `Assets/Scripts/CustomDropdown.cs` (DropDownList)
- `Assets/Scripts/PointsManager.cs` (Points Manager)
- `Assets/Scripts/LevelSelection.cs` (Points Manager)
- `Assets/Scripts/NameTag.cs` (Name)
- `Assets/Scripts/CharacterSelection.cs` (Characters)
- `Assets/Scripts/LocaleSelector.cs` (Localization Manager)
- `Assets/Scripts/TextUpdater.cs` (Localization Manager)

Scene Boot & State
- `Assets/Scripts/StarsHandler.cs` (CoffeeButton)
- `Assets/Scripts/PointsManager.cs` (Points Manager)
- `Assets/Scripts/LevelSelection.cs` (Points Manager)

Interaction & Input
- None

Task & Objective Scripts
- `Assets/Scripts/StarsHandler.cs` (CoffeeButton)

Guidance & UI
- `Assets/Scripts/NameTag.cs` (Name)
- `Assets/Scripts/TextUpdater.cs` (Localization Manager)

Dialogue & Localization
- `Assets/Scripts/LocaleSelector.cs` (Localization Manager)

Menu / Selection
- `Assets/Scripts/UsernameSelection.cs` (GameObject(869670615))
- `Assets/Scripts/CustomDropdown.cs` (DropDownList)
- `Assets/Scripts/CharacterSelection.cs` (Characters)

Flow (high level)
```text
[Scene Load]
-> Scene boot (LoadCharacter/AppManager/MenuHandler/Points/Stars)
-> Guidance/UI (ArrowHandler/OutlinePulse/Canvas*)
-> Dialogue/Audio (LocalizedAudioChanger/PlayLocalizedAudio)
-> Menu/Selection (CharacterSelection/UsernameSelection/CustomDropdown)
```

## SecondLevel.unity

Scene file: `Assets/Scenes/SecondLevel.unity`

Highlights
- `Assets/Scripts/ArrowHandler.cs` (ISDK_HandGrabInteraction)
- `Assets/Scripts/CoffeeFill.cs` (CoffeeFillTrigger)
- `Assets/Scripts/SpecificSnapTargetFilter.cs` (ISDK_HandGrabInteraction)
- `Assets/Scripts/SnapEventsHandler.cs` (ISDK_HandGrabInteraction)
- `Assets/Scripts/SimpleHandDetection.cs` (ISDK_HandGrabInteraction)
- `Assets/Scripts/PlayLocalizedAudio.cs` (ManAudio)
- `Assets/Scripts/LocalizedAudioChanger.cs` (ManAudio)
- `Assets/Scripts/StarsHandler.cs` (Scene Manager)
- `Assets/Scripts/ScoreGrabThing.cs` (Scene Manager)
- `Assets/Scripts/ScoreGrabAndPlaceThing.cs` (Scene Manager)
- `Assets/Scripts/ArmExtensionDetector.cs` (Scene Manager)
- `Assets/Scripts/MenuHandler.cs` (Scene Manager)
- `Assets/Scripts/AppManager.cs` (Scene Manager)
- `Assets/Scripts/CollisionManager.cs` (Scene Manager)
- `Assets/Scripts/LoadCharacter.cs` (Scene Manager)
- `Assets/Scripts/SpecificSnapInteractorFilter.cs` (LidAnchor)
- `Assets/Scripts/OutlinePulse.cs` (PlateOutline)
- `Assets/Scripts/CharacterSelection.cs` (NextLvlButton)
- `Assets/Scripts/CoffeeMachineHelperFunctions.cs` (GameObject(880475557))
- `Assets/Scripts/DebugInfoHandler.cs` (GameObject(1344720508))

Scene Boot & State
- `Assets/Scripts/StarsHandler.cs` (Scene Manager)
- `Assets/Scripts/ArmExtensionDetector.cs` (Scene Manager)
- `Assets/Scripts/MenuHandler.cs` (Scene Manager)
- `Assets/Scripts/AppManager.cs` (Scene Manager)
- `Assets/Scripts/CollisionManager.cs` (Scene Manager)
- `Assets/Scripts/LoadCharacter.cs` (Scene Manager)

Interaction & Input
- `Assets/Scripts/SpecificSnapTargetFilter.cs` (ISDK_HandGrabInteraction)
- `Assets/Scripts/SnapEventsHandler.cs` (ISDK_HandGrabInteraction)
- `Assets/Scripts/SimpleHandDetection.cs` (ISDK_HandGrabInteraction)
- `Assets/SnapInteractionAssets/Scripts/SnapInteractableVisuals.cs` (PlateAnchor)
- `Assets/Scripts/SpecificSnapInteractorFilter.cs` (LidAnchor)

Task & Objective Scripts
- `Assets/GrabLidObjective.cs` (ISDK_HandGrabInteraction)
- `Assets/Scripts/ArrowHandler.cs` (ISDK_HandGrabInteraction)
- `Assets/Scripts/CoffeeFill.cs` (CoffeeFillTrigger)
- `Assets/Scripts/SnapEventsHandler.cs` (ISDK_HandGrabInteraction)
- `Assets/Scripts/StarsHandler.cs` (Scene Manager)
- `Assets/Scripts/ScoreGrabThing.cs` (Scene Manager)
- `Assets/Scripts/ScoreGrabAndPlaceThing.cs` (Scene Manager)
- `Assets/Scripts/MenuHandler.cs` (Scene Manager)
- `Assets/Scripts/CoffeeMachineHelperFunctions.cs` (GameObject(880475557))
- `Assets/Scripts/DebugInfoHandler.cs` (GameObject(1344720508))

Guidance & UI
- `Assets/Scripts/ArrowHandler.cs` (ISDK_HandGrabInteraction)
- `Assets/Scripts/OutlinePulse.cs` (PlateOutline)

Dialogue & Localization
- `Assets/Scripts/PlayLocalizedAudio.cs` (ManAudio)
- `Assets/Scripts/LocalizedAudioChanger.cs` (ManAudio)

Menu / Selection
- `Assets/Scripts/CharacterSelection.cs` (NextLvlButton)

Flow (high level)
```text
[Scene Load]
-> Scene boot (LoadCharacter/AppManager/MenuHandler/Points/Stars)
-> Task chain (GrabLidObjective/ScoreGrabThing -> Snap + placement -> CoffeeFill)
-> Objective checks (SnapEventsHandler/SpecificSnap* + Outline/Arrow guidance)
-> Guidance/UI (ArrowHandler/OutlinePulse/Canvas*)
-> Dialogue/Audio (LocalizedAudioChanger/PlayLocalizedAudio)
-> Menu/Selection (CharacterSelection/UsernameSelection/CustomDropdown)
```

## StarterMenu.unity

Scene file: `Assets/Scenes/StarterMenu.unity`

Highlights
- `Assets/Scripts/LocaleSelector.cs` (LocalizationManager)
- `Assets/Scripts/CharacterSelection.cs` (Characters)

Scene Boot & State
- None

Interaction & Input
- None

Task & Objective Scripts
- None

Guidance & UI
- None

Dialogue & Localization
- `Assets/Scripts/LocaleSelector.cs` (LocalizationManager)

Menu / Selection
- `Assets/Scripts/CharacterSelection.cs` (Characters)

Flow (high level)
```text
[Scene Load]
-> Dialogue/Audio (LocalizedAudioChanger/PlayLocalizedAudio)
-> Menu/Selection (CharacterSelection/UsernameSelection/CustomDropdown)
```

## TestScene.unity

Scene file: `Assets/Scenes/TestScene.unity`

Highlights
- `Assets/Scripts/VacuumCleaningHandler.cs` (Dust (5))
- `Assets/Scripts/SimpleHandDetection.cs` (ISDK_HandGrabInteraction)
- `Assets/Scripts/CanvasNumberUpdater.cs` (Particle_Spray)
- `Assets/Scripts/ArrowHandler.cs` (PaperCrane 6)
- `Assets/Scripts/RemoveSpotsOnWindowObjective.cs` (GameObject(435870247))
- `Assets/Scripts/PaperInBinObjective.cs` (GameObject(660484309))
- `Assets/Scripts/CharacterSelection.cs` (CoffeeLvlButton)
- `Assets/Scripts/PlayLocalizedAudio.cs` (WomanAudio)
- `Assets/Scripts/LocalizedAudioChanger.cs` (WomanAudio)
- `Assets/Scripts/OutlinePulse.cs` (VacuumButtonOutline)
- `Assets/Scripts/DebugInfoHandler.cs` (GameObject(1424844024))
- `Assets/Scripts/StarsHandler.cs` (Scene Manager)
- `Assets/Scripts/ArmExtensionDetector.cs` (Scene Manager)
- `Assets/Scripts/AppManager.cs` (Scene Manager)
- `Assets/Scripts/CollisionManager.cs` (Scene Manager)
- `Assets/Scripts/LoadCharacter.cs` (Scene Manager)
- `Assets/Scripts/ScreenFader.cs` (Scene Manager)

Scene Boot & State
- `Assets/Scripts/StarsHandler.cs` (Scene Manager)
- `Assets/Scripts/ArmExtensionDetector.cs` (Scene Manager)
- `Assets/Scripts/AppManager.cs` (Scene Manager)
- `Assets/Scripts/CollisionManager.cs` (Scene Manager)
- `Assets/Scripts/LoadCharacter.cs` (Scene Manager)
- `Assets/Scripts/ScreenFader.cs` (Scene Manager)

Interaction & Input
- `Assets/Scripts/SimpleHandDetection.cs` (ISDK_HandGrabInteraction)

Task & Objective Scripts
- `Assets/Scripts/VacuumCleaningHandler.cs` (Dust (5))
- `Assets/SprayCleanerHandler.cs` (Particle_Spray)
- `Assets/DirtOnGlassHandler.cs` (Splat04)
- `Assets/PaperBallHandler.cs` (PaperCrane 6)
- `Assets/Scripts/ArrowHandler.cs` (PaperCrane 6)
- `Assets/Scripts/RemoveSpotsOnWindowObjective.cs` (GameObject(435870247))
- `Assets/Scripts/PaperInBinObjective.cs` (GameObject(660484309))
- `Assets/VacuumCleanerHandler.cs` (ISDK_PokeInteraction)
- `Assets/Scripts/DebugInfoHandler.cs` (GameObject(1424844024))
- `Assets/Scripts/StarsHandler.cs` (Scene Manager)

Guidance & UI
- `Assets/Scripts/CanvasNumberUpdater.cs` (Particle_Spray)
- `Assets/Scripts/ArrowHandler.cs` (PaperCrane 6)
- `Assets/Scripts/OutlinePulse.cs` (VacuumButtonOutline)

Dialogue & Localization
- `Assets/Scripts/PlayLocalizedAudio.cs` (WomanAudio)
- `Assets/Scripts/LocalizedAudioChanger.cs` (WomanAudio)

Menu / Selection
- `Assets/Scripts/CharacterSelection.cs` (CoffeeLvlButton)

Flow (high level)
```text
[Scene Load]
-> Scene boot (LoadCharacter/AppManager/MenuHandler/Points/Stars)
-> Task chain (cleaning objectives: vacuum/spray/paper) driven by triggers + OnCollision)
-> Objective checks (DirtOnGlassHandler/PaperInBinObjective/RemoveSpotsOnWindowObjective)
-> Guidance/UI (ArrowHandler/OutlinePulse/Canvas*)
-> Dialogue/Audio (LocalizedAudioChanger/PlayLocalizedAudio)
-> Menu/Selection (CharacterSelection/UsernameSelection/CustomDropdown)
```

## ThirdLevel.unity

Scene file: `Assets/Scenes/ThirdLevel.unity`

Highlights
- `Assets/Scripts/PaperInBinObjective.cs` (GameObject(726567646))
- `Assets/Scripts/StarsHandler.cs` (Scene Manager)
- `Assets/Scripts/ArmExtensionDetector.cs` (Scene Manager)
- `Assets/Scripts/AppManager.cs` (Scene Manager)
- `Assets/Scripts/CollisionManager.cs` (Scene Manager)
- `Assets/Scripts/LoadCharacter.cs` (Scene Manager)
- `Assets/Scripts/SimpleHandDetection.cs` (HandGrabInteractable)
- `Assets/Scripts/PlayLocalizedAudio.cs` (ManAudio)
- `Assets/Scripts/LocalizedAudioChanger.cs` (ManAudio)
- `Assets/Scripts/VacuumCleaningHandler.cs` (Dust)

Scene Boot & State
- `Assets/Scripts/StarsHandler.cs` (Scene Manager)
- `Assets/Scripts/ArmExtensionDetector.cs` (Scene Manager)
- `Assets/Scripts/AppManager.cs` (Scene Manager)
- `Assets/Scripts/CollisionManager.cs` (Scene Manager)
- `Assets/Scripts/LoadCharacter.cs` (Scene Manager)

Interaction & Input
- `Assets/Scripts/SimpleHandDetection.cs` (HandGrabInteractable)

Task & Objective Scripts
- `Assets/SprayCleanerHandler.cs` (Particle_Stream)
- `Assets/Scripts/PaperInBinObjective.cs` (GameObject(726567646))
- `Assets/DirtOnGlassHandler.cs` (Splat07)
- `Assets/Scripts/StarsHandler.cs` (Scene Manager)
- `Assets/VacuumCleanerHandler.cs` (ISDK_PokeInteraction)
- `Assets/Scripts/VacuumCleaningHandler.cs` (Dust)

Guidance & UI
- None

Dialogue & Localization
- `Assets/Scripts/PlayLocalizedAudio.cs` (ManAudio)
- `Assets/Scripts/LocalizedAudioChanger.cs` (ManAudio)

Menu / Selection
- None

Flow (high level)
```text
[Scene Load]
-> Scene boot (LoadCharacter/AppManager/MenuHandler/Points/Stars)
-> Task chain (cleaning objectives: vacuum/spray/paper) driven by triggers + OnCollision)
-> Objective checks (DirtOnGlassHandler/PaperInBinObjective/RemoveSpotsOnWindowObjective)
-> Dialogue/Audio (LocalizedAudioChanger/PlayLocalizedAudio)
```

## UISet.unity

Scene file: `Assets/Scenes/UISet.unity`

Highlights
- No custom scripts detected

Scene Boot & State
- None

Interaction & Input
- None

Task & Objective Scripts
- None

Guidance & UI
- None

Dialogue & Localization
- None

Menu / Selection
- None

Flow (high level)
```text
[Scene Load]
```

## WallPaintingScene.unity

Scene file: `Assets/Scenes/WallPaintingScene.unity`

Highlights
- `Assets/Scripts/ActivatePaint.cs` (Bristles_Collider)
- `Assets/Scripts/WallPainting.cs` (Bristles_Collider)

Scene Boot & State
- None

Interaction & Input
- None

Task & Objective Scripts
- `Assets/Scripts/ActivatePaint.cs` (Bristles_Collider)
- `Assets/Scripts/WallPainting.cs` (Bristles_Collider)

Guidance & UI
- None

Dialogue & Localization
- None

Menu / Selection
- None

Flow (high level)
```text
[Scene Load]
-> Task chain (ActivatePaint -> WallPainting)
```
