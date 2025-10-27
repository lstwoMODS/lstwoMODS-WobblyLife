using BepInEx.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Technie.PhysicsCreator;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.Widgets.ScrollView;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;
using System.Linq;
using UnityExplorer;
using UnityExplorer.UI;
using HawkNetworking;

namespace lstwoMODS_WobblyLife.UI.TabMenus;

public static class PropAddressData
{
    public static Dictionary<string, object> AddressTree = new();

    /*public static readonly Dictionary<string, object> AddressTree = new()
    {
        {
            "Game/Prefabs/", new Dictionary<string, object>
            {
                {
                    "Props/", new Dictionary<string, object>
                    {
                        {
                            "Artifacts/", new string[]
                            {
                                "Ancient Fossils/Artifact_AncientFossils_Ammonite",
                                "Ancient Fossils/Artifact_AncientFossils_Fossilised_Plant",
                                "Ancient Fossils/Artifact_AncientFossils_Giant_Shell",
                                "Ancient Fossils/Artifact_AncientFossils_Shark Tooth",
                                "Ancient Fossils/Artifact_AncientFossils_Trilobite", "Cave/Artifact Cave Amethyst",
                                "Cave/Artifact Cave Jade", "Cave/Artifact Cave Ruby",
                                "Cave/Artifact Cave Saphire", "Dinosaur Fossils/Artifact_Dino Egg",
                                "Dinosaur Fossils/Artifact_Dino Footprint",
                                "Dinosaur Fossils/Artifact_Dino_Claw", "Dinosaur Fossils/Artifact_Dino_Dropping",
                                "Dinosaur Fossils/Artifact_Dino_Mosquito_in_Amber_resin",
                                "Early Wobbly/Artifact_EarlyWobbly_EarlyWobblySpear",
                                "Early Wobbly/Artifact_EarlyWobbly_First_Pizza",
                                "Early Wobbly/Artifact_EarlyWobbly_FirstWheel",
                                "Early Wobbly/Artifact_EarlyWobbly_Fossilised_Present",
                                "Early Wobbly/Artifact_EarlyWobbly_Wobbly_Club", "Gold/Artifact_Gold_Gold_Pants",
                                "Gold/Artifact_Gold_Gold_Ring", "Gold/Artifact_Gold_Wobbly_Amulet",
                                "Gold/Artifact_Gold_Wobbly_Sceptre", "Ice Cave/Artifact_IceCave_Mamoth Tusk",
                                "Ice Cave/Artifact_IceCave_Penguin Hockey Stick",
                                "Ice Cave/Artifact_IceCave_Penguin Ice Sculpture", "Ice Cave/Artifact_IceCave_Yeti Fur",
                                "Jungle/Artifact_Jungle_Ancient_Wobbly_Bomb",
                                "Jungle/Artifact_Jungle_Ancient_Wobbly_Mask", "Jungle/Artifact_Jungle_Venus_FlyTrap",
                                "Jungle/Artifact_Jungle_Wobbly_Stone", "Ocean/Artifact_Ocean_ClamShell",
                                "Ocean/Artifact_Ocean_Coral", "Ocean/Artifact_Ocean_Pearl",
                                "Ocean/Artifact_Ocean_Turtle Shell", "ParadiseIsland/Artifact_ParadiseIsland_Cocktail",
                                "ParadiseIsland/Artifact_ParadiseIsland_Flower",
                                "ParadiseIsland/Artifact_ParadiseIsland_Golf_Ball_Emerald",
                                "ParadiseIsland/Artifact_ParadiseIsland_GolfClub_Gold",
                                "Random/Artifact_Random_Ancient_Jelly", "Random/Artifact_Random_Ghost_Anchor",
                                "Sewer/Artifact_Sewer_Baby Tooth", "Sewer/Artifact_Sewer_Blueprints",
                                "Sewer/Artifact_Sewer_Goldern Toilet", "Sewer/Artifact_Sewer_Lipstick",
                                "Treasure/Artifact_Treasure_Ancient_Crown",
                                "Treasure/Artifact_Treasure_Ancient_Scroll", "Treasure/Artifact_Treasure_Goblet",
                                "Treasure/Artifact_Treasure_UraniumGems",
                                "TrexParts/Artifact_Trex_ArmPart", "TrexParts/Artifact_Trex_JawPart",
                                "TrexParts/Artifact_Trex_LegPart", "TrexParts/Artifact_Trex_TailPart",
                                "Wizard/Artifact_Wizard_Hat", "Wizard/Artifact_Wizard_Magic Book",
                                "Wizard/Artifact_Wizard_Robe", "Wizard/Artifact_Wizard_Staff"
                            }
                        },
                        {
                            "HouseInterior/", new string[]
                            {
                                "Grannys Sofa", "Prop_Big_Chair_01", "Prop_Big_Chair_02", "Prop_Big_Chair_03",
                                "Prop_Big_Clock", "Prop_Chair_01", "Prop_Chair_02", "Prop_Chair_03", "Prop_Chair_04",
                                "Prop_Chair_05",
                                "Prop_Chair_06", "Prop_Chair_07", "Prop_Chair_08", "Prop_Chair_09", "Prop_Couch_01",
                                "Prop_Couch_02", "Prop_Couch_03", "Prop_Couch_04", "Prop_Couch_05", "Prop_Couch_06",
                                "Prop_Couch_07", "Prop_Couch_08", "Prop_Couch_09", "Prop_Cushion_01", "Prop_Cushion_02",
                                "Prop_Desk_Chair", "Prop_Draw_01", "Prop_Draw_02", "Prop_Draw_03", "Prop_Draw_04",
                                "Prop_Draw_05", "Prop_Draw_06", "Prop_Dryer", "Prop_Flower_Pot_01", "Prop_Foot_Rest_01",
                                "Prop_Foot_Rest_02", "Prop_Fridge_01", "Prop_FruitBowl", "Prop_Shelf_07_Dynamic",
                                "Prop_Small_Table_01",
                                "Prop_Small_Table_02", "Prop_Small_Table_03", "Prop_Small_Table_04",
                                "Prop_Small_Table_05", "Prop_Small_Table_06", "Prop_Small_Table_08", "Prop_Speaker_01",
                                "Prop_Speaker_02", "Prop_Speaker_03", "Prop_Table_01",
                                "Prop_Table_02", "Prop_Table_03", "Prop_Tv_01", "Prop_Tv_02", "Prop_Tv_03",
                                "Prop_Tv_04", "Prop_Tv_06", "Prop_Tv_07", "Prop_Washing_Machine", "Prop_Cupboard_01",
                                "Prop_Cupboard_02", "Prop_Cupboard_03", "Prop_Cupboard_04", "Prop_Cupboard_07",
                                "Prop_Lamp_01", "Prop_Lamp_02", "Prop_Lamp_03", "Prop_Lamp_04", "Prop_Lamp_05",
                                "Prop_Lamp_06",
                                "Prop_Microwave", "Prop_Oven_Blue", "Prop_Oven_Grey", "Prop_Oven_White"
                            }
                        },
                        {
                            "GoldMine/", new string[]
                            {
                                "PickAxe", "Rocks/Diamond_Rock_Large", "Rocks/Diamond_Rock_Normal",
                                "Rocks/Diamond_Rock_Small", "Rocks/Ghost_GraveStone", "Rocks/Gold_Rock_Large",
                                "Rocks/Gold_Rock_Normal", "Rocks/Gold_Rock_Small", "Rocks/Iron_Rock_Large",
                                "Rocks/Iron_Rock_Normal",
                                "Rocks/Iron_Rock_Small", "Rocks/Uranium_Rock_Large", "Rocks/Uranium_Rock_Normal",
                                "Rocks/Uranium_Rock_Small", "TNT"
                            }
                        },
                        {
                            "Hospital/", new string[]
                            {
                                "HospitalBloodPresure", "HospitalMachine", "HospitalStand"
                            }
                        },
                        {
                            "Wizard/", new string[]
                            {
                                "Wizard Prop Shop/MagicWand_Attract", "Wizard Prop Shop/MagicWand_Repel",
                                "Wizard Prop Shop/WizardPotion_BigHead",
                                "Wizard Prop Shop/WizardPotion_BigNose", "Wizard Prop Shop/WizardPotion_Fart",
                                "Wizard Prop Shop/WizardPotion_FlyAway",
                                "Wizard Prop Shop/WizardPotion_Jump", "Wizard Prop Shop/WizardPotion_LongArms",
                                "Wizard Prop Shop/WizardPotion_RainbowSkin",
                                "Wizard Prop Shop/WizardPotion_Sleep", "Wizard Prop Shop/WizardPotion_SmallHead",
                                "Wizard Prop Shop/WizardPotion_Speed",
                                "Wizard Prop Shop/WizardPotionSack", "WizardTent"
                            }
                        },
                        {
                            "Farm/", new string[]
                            {
                                "Bean", "CauliFlower", "Cow_Dynamic", "SweetCorn", "Tomato", "Tomato_Cannon"
                            }
                        },
                        {
                            "Fishing_Props/", new string[]
                            {
                                "Fish/Dynamic/Angel Fish_Dynamic", "Fish/Dynamic/Angler Fish_Dynamic",
                                "Fish/Dynamic/Arctic Char_Dynamic", "Fish/Dynamic/Arctic Cod_Dynamic",
                                "Fish/Dynamic/Boot_Dynamic", "Fish/Dynamic/Bream_Dynamic",
                                "Fish/Dynamic/Blue Dragon_Dynamic", "Fish/Dynamic/Carp_Dynamic",
                                "Fish/Dynamic/CatFish_Dynamic", "Fish/Dynamic/Cockle_Dynamic",
                                "Fish/Dynamic/ClownFish_Dynamic", "Fish/Dynamic/Cod_Dynamic",
                                "Fish/Dynamic/Eel_Dynamic", "Fish/Dynamic/FlyingFish_Dynamic",
                                "Fish/Dynamic/Flounder_Dynamic", "Fish/Dynamic/Frozen Boot",
                                "Fish/Dynamic/Giant Crab_Dynamic", "Fish/Dynamic/Grouper_Dynamic",
                                "Fish/Dynamic/Haddock_Dynamic", "Fish/Dynamic/Herring_Dynamic",
                                "Fish/Dynamic/Ice Cube Fish_Dynamic", "Fish/Dynamic/Ice Cube_Dynamic",
                                "Fish/Dynamic/Ice Fish_Dynamic", "Fish/Dynamic/JellyFish_Dynamic",
                                "Fish/Dynamic/LionFish_Dynamic", "Fish/Dynamic/Lobster_Dynamic",
                                "Fish/Dynamic/Marlin_Dynamic", "Fish/Dynamic/Oyster_Dynamic",
                                "Fish/Dynamic/Pike_Dynamic", "Fish/Dynamic/Prawn_Dynamic",
                                "Fish/Dynamic/Pufferfish_Dynamic", "Fish/Dynamic/Rainbow Trout_Dynamic",
                                "Fish/Dynamic/Salmon_Dynamic", "Fish/Dynamic/Salmon_Pink_Dynamic",
                                "Fish/Dynamic/Sardine_Dynamic", "Fish/Dynamic/Sea Cucumber_Dynamic",
                                "Fish/Dynamic/SeaHorse_Green_Dynamic", "Fish/Dynamic/SeaHorse_Yellow_Dynamic",
                                "Fish/Dynamic/Sea Spider_Dynamic", "Fish/Dynamic/SeaUrchin_Dynamic",
                                "Fish/Dynamic/Seaweed Ball_Dynamic", "Fish/Dynamic/Squid_Dynamic",
                                "Fish/Dynamic/Starfish_Dynamic", "Fish/Dynamic/StingRay_Dynamic",
                                "Fish/Dynamic/Trout_Dynamic", "Fish/Dynamic/Wobbly Fish_Dynamic",
                                "Fish/Dynamic/Worm_Dynamic"
                            }
                        },
                        {
                            "Burger/", new string[]
                            {
                                "BottomBun", "BurgerCooked", "BurgerRaw", "Cheese", "CheeseMelted", "Lettuce", "Tomato",
                                "TopBun"
                            }
                        },
                        {
                            "Caves/", new string[]
                            {
                                "Cave_CoinRoom/Coins/Coin_Moon", "Cave_CoinRoom/Coins/Coin_MoonStar",
                                "Cave_CoinRoom/Coins/Coin_Sun", "Cave_CoinRoom/Coins/Coin_Sun_1",
                                "Cave_CoinRoom/Coins/Coin_Stars_Moon", "HauntedTunnel/FlashLight", "IceCave/GongHammer",
                                "IceCave/Hockey Stick", "IceCave/IceChisel",
                                "IceCave/IcePick", "IceCave/Yeti Photo Frame"
                            }
                        },
                        {
                            "Shopping_Baskets/", new string[]
                            {
                                "Green_ShoppingBasket"
                            }
                        },
                        {
                            "ShopInterior/", new string[]
                            {
                                "SI_Prop_Bench_01", "SI_Prop_CarboardBox_01", "SI_Prop_CarboardBox_02",
                                "SI_Prop_CarboardBox_03", "SI_Prop_Chair_01", "SI_Prop_FireExtinguisher_01",
                                "SI_Prop_HighChair_01", "SI_Prop_Instrument_Guitar_01", "SI_Prop_Instrument_Guitar_02",
                                "SI_Prop_Instrument_Guitar_03",
                                "SI_Prop_Instrument_Guitar_04", "SI_Prop_Instrument_Guitar_06",
                                "SI_Prop_Instrument_Keyboard", "SI_Prop_Instrument_Keyboard (1)", "SI_Prop_Ladder_01",
                                "SI_Prop_SignCaution_01", "SI_Prop_Stereo", "SI_Prop_Stereo_03", "SI_Prop_Toy_Alien_1",
                                "SI_Prop_Toy_Banana",
                                "SI_Prop_Toy_Bear_01", "SI_Prop_Toy_Bear_02", "SI_Prop_Toy_Caterpillar",
                                "SI_Prop_Toy_Chick_01", "SI_Prop_Toy_Dog_01", "SI_Prop_Toy_Leak"
                            }
                        },
                        {
                            "TvStudio/", new string[]
                            {
                                "Chair", "DirectorsChair", "DynamicWobblyQuiz_Poster", "Wig_Hair_Dynamic"
                            }
                        },
                        {
                            "Construction/", new string[]
                            {
                                "Props/ClawHammer", "Props/LumpHammer", "Props/ResourcesBag_Dynamic"
                            }
                        },
                        {
                            "BoardGames/", new string[]
                            {
                                "BluePiece", "CheckersRed", "CheckersWhite", "Chess Pieces/Chess Bishop Black",
                                "Chess Pieces/Chess Bishop White", "Chess Pieces/Chess King Black",
                                "Chess Pieces/Chess King White", "Chess Pieces/Chess Knight Black",
                                "Chess Pieces/Chess Knight White", "Chess Pieces/Chess Pawn Black",
                                "Chess Pieces/Chess Pawn White", "Chess Pieces/Chess Queen Black",
                                "Chess Pieces/Chess Queen White", "Chess Pieces/Chess Rook Black",
                                "Chess Pieces/Chess Rook White", "Dice", "GreenPiece", "RedPiece", "YellowPiece"
                            }
                        },
                        {
                            "WeatherStation/", new string[]
                            {
                                "Job/WeatherBalloon_Device_ZigZag"
                            }
                        },
                        {
                            "Treasure/", new string[]
                            {
                                "MetalDetector", "Money/Treasure_Bronze_Coin", "Money/Treasure_Bronze_Ingot",
                                "Money/Treasure_Bronze_Ring", "Money/Treasure_Gold_Coin",
                                "Money/Treasure_Gold_Ingot", "Money/Treasure_Silver Goblet",
                                "Money/Treasure_Silver_Coin", "Money/Treasure_Silver_Ingot",
                                "Money/Treasure_Silver_Ring"
                            }
                        },
                        {
                            "Dream/", new string[]
                            {
                                "Castle/AlarmClock", "Castle/Bedroom/StoryCounters/StoryCounter_1",
                                "Castle/Bedroom/StoryCounters/StoryCounter_2",
                                "Castle/Bedroom/StoryCounters/StoryCounter_3",
                                "Castle/Bedroom/StoryCounters/StoryCounter_4",
                                "Castle/Bedroom/StoryCounters/StoryCounter_5",
                                "Castle/ClockKey", "Castle/IdeaBulb_SofaUnlock", "Castle/Kitchen/Dream_Kitchen_Apple",
                                "Castle/Kitchen/Dream_Kitchen_Bread",
                                "Castle/Kitchen/Dream_Kitchen_Carrot", "Castle/Kitchen/Dream_Kitchen_Cheese",
                                "Castle/Kitchen/Dream_Kitchen_Fish_1",
                                "Castle/Kitchen/Dream_Kitchen_Ham", "Castle/Kitchen/Dream_Kitchen_LargeCake",
                                "Castle/Kitchen/Dream_Kitchen_Lettuce",
                                "Diorama Props/Dream Puzzle Golf Club_Dynamic",
                                "Diorama Props/Dream Puzzle Ham_Dynamic", "Diorama Props/Dream Puzzle Shovel_Dynamic",
                                "Diorama Props/Dream Puzzle Sloth_Dynamic",
                                "Diorama Props/Dream Puzzle TowerBlock_Dynamic",
                                "Diorama Props/Dream Puzzle TrexBone_Dynamic",
                                "Ideas/Dream_Idea_Prop_Burger", "Ideas/Dream_Idea_Prop_Duck",
                                "Ideas/Dream_Idea_Prop_Piano", "Ideas/Dream_Idea_Prop_Plane", "KingsInvitation"
                            }
                        },
                        {
                            "Ice Cream/", new string[]
                            {
                                "Ice Cream Bucket", "Ice Cream Scoop"
                            }
                        },
                        {
                            "JellyMan/", new string[]
                            {
                                "JackHammer", "JellyBasementKey", "JellyCarSteeringWheel", "JellyCar_Wheel",
                                "JellyChair", "JellyLamp", "JellySofa", "NoJellyCarEngineDynamic"
                            }
                        },
                        {
                            "PowerPlant/", new string[]
                            {
                                "ToxicWaste_Drum"
                            }
                        },
                        {
                            "PlayGround/", new string[]
                            {
                                "BeachBall", "BeachBall_Large", "BeachBall_Mid", "BouncyCastle", "FootBall",
                                "Horse on stick", "Rocking horse", "Toy_Helicopter", "Trampoline"
                            }
                        },
                        {
                            "Observatory/", new string[]
                            {
                                "Observatory_Moon Boots Box"
                            }
                        },
                        {
                            "Museum/", new string[]
                            {
                                "MuseumBasement/TrexBone", "MuseumBasement/Trilobite"
                            }
                        },
                        {
                            "Lab/", new string[]
                            {
                                "LargeLab/Baby Spider", "LargeLab/BluePiece", "LargeLab/CheckersRed",
                                "LargeLab/CheckersWhite", "LargeLab/Dice", "LargeLab/GreenPiece",
                                "LargeLab/PushPin", "LargeLab/RedPiece", "LargeLab/SI_Prop_CarboardBox_02",
                                "LargeLab/Straw", "LargeLab/YellowPiece", "UFO Part Battery",
                                "UFO Part Fuse", "UFO Part Keys"
                            }
                        },
                        {
                            "Lumber/", new string[]
                            {
                                "LumberTree_Dynamic"
                            }
                        },
                        {
                            "MountainBase/", new string[]
                            {
                                "Alien Proof", "GiantBoxes/GiantBoxes.008", "GiantBoxes/GiantBoxes.009",
                                "GiantBoxes/GiantBoxes.010", "GiantBoxes/GiantBoxes.012",
                                "ScienceSmallContainer"
                            }
                        },
                        {
                            "Missions/", new string[]
                            {
                                "Detective Series/HauntedHouse/HauntedHouseLunchBox",
                                "Detective Series/LostMagnifyingGlass/MagnifyingGlass",
                                "Detective Series/VigilanteJelly/CleaningProduct",
                                "Detective Series/VigilanteJelly/HandBag_Jelly",
                                "Diserted Island/Bouncy Ball With Face",
                                "Dress As Clown Party/Baloon Inflator", "Dress As Clown Party/Baloon Snake",
                                "Dress As Clown Party/Baloon Sword",
                                "Dress As Clown Party/Clown Cream Pie", "Golden Bowling/Bowling_Ball_Golden",
                                "JungleGorilla/Banana_JungleGorillaMission",
                                "LostBeesMission/Bee Keeper Net", "LostBinoculars/Binoculars",
                                "Stolen Sandwich/Sandwich_MissionItem", "Unrobbery Bank/Bank_Card_Blue",
                                "Unrobbery Bank/Bank_Card_Red", "Unrobbery Bank/Bank Diamond",
                                "Unrobbery Bank/BankFuseBox_Key", "Unrobbery Bank/BankVase",
                                "UnderGroundMixup/FirstAidKit", "UnderGroundMixup/FloatyRescueDuck",
                                "UnderGroundMixup/MonkeyWrench"
                            }
                        },
                        {
                            "Sewers/", new string[]
                            {
                                "Sewer Barrel Blue", "Sewer Barrel Toxic Waste", "Sewer Lanturn Handheld",
                                "Sewer Old/Queen Frog Crown", "Sewer Puzzle/Sewer_Mountain/Coal",
                                "Sewer Puzzle/Sewer_Mountain/Plaque_Left", "Sewer Puzzle/Sewer_Mountain/Plaque_Right"
                            }
                        },
                        {
                            "Jungle/", new string[]
                            {
                                "Game/Cave_Maze_Key_Orb", "Game/Moving_Block", "Game/Temple_Maze_Key_Orb",
                                "Ruins/NaughtsAndCrosses_O", "Ruins/NaughtsAndCrosses_X", "Temple/Crate",
                                "Temple/FloorTorch", "Temple/Temple_TallVase", "Temple/Temple_Vase",
                                "TrialRooms/Trial Hat Prop"
                            }
                        },
                        {
                            "PropShop/", new string[]
                            {
                                "PropShop_Food/PropShop_Apple", "PropShop_Food/PropShop_Baguette",
                                "PropShop_Food/PropShop_Banana", "PropShop_Food/PropShop_Barrito",
                                "PropShop_Food/PropShop_Bread", "PropShop_Food/PropShop_BottleWater",
                                "PropShop_Food/PropShop_CakeSlice", "PropShop_Food/PropShop_Carrot",
                                "PropShop_Food/PropShop_CauliFlower", "PropShop_Food/PropShop_Cereal",
                                "PropShop_Food/PropShop_Cheese", "PropShop_Food/PropShop_Chicken",
                                "PropShop_Food/PropShop_Chocolate", "PropShop_Food/PropShop_ChocolateLolly",
                                "PropShop_Food/PropShop_CrispyCrisps",
                                "PropShop_Food/PropShop_Croissant", "PropShop_Food/PropShop_Cup",
                                "PropShop_Food/PropShop_Donut", "PropShop_Food/PropShop_Eclair",
                                "PropShop_Food/PropShop_Fish", "PropShop_Food/PropShop_Ham",
                                "PropShop_Food/PropShop_HotDog", "PropShop_Food/PropShop_IceCreamCone",
                                "PropShop_Food/PropShop_IceCreamSoft", "PropShop_Food/PropShop_IceCreamTub",
                                "PropShop_Food/PropShop_IceLolly", "PropShop_Food/PropShop_IceSlice",
                                "PropShop_Food/PropShop_LargeCake", "PropShop_Food/PropShop_Lettuce",
                                "PropShop_Food/PropShop_Milk", "PropShop_Food/PropShop_Pizza",
                                "PropShop_Food/PropShop_Sandwich", "PropShop_Food/PropShop_SodaPop",
                                "PropShop_Food/PropShop_Steak", "PropShop_Food/PropShop_SweetCorn",
                                "PropShop_Food/PropShop_Tomato", "PropShop_Food/PropShop_TwoStickIceLolly",
                                "PropShop_Food/PropShop_VeggieSausage",
                                "PropShop_Food/PropShop_WaterMelon", "PropShop_GreenScreen/PropShop_Anchor",
                                "PropShop_GreenScreen/PropShop_ClapBoard",
                                "PropShop_GreenScreen/PropShop_MoonRock", "PropShop_GreenScreen/PropShop_MoonRock_1",
                                "PropShop_GreenScreen/PropShop_MessageBottle",
                                "PropShop_GreenScreen/PropShop_Prop_Cactus",
                                "PropShop_GreenScreen/PropShop_Prop_Cactus_1",
                                "PropShop_GreenScreen/PropShop_Prop_CampFire",
                                "PropShop_GreenScreen/PropShop_Prop_CattleSkull",
                                "PropShop_GreenScreen/PropShop_Prop_Crab", "PropShop_GreenScreen/PropShop_Prop_Flag",
                                "PropShop_GreenScreen/PropShop_Prop_MoonBuggy",
                                "PropShop_GreenScreen/PropShop_Prop_Rock", "PropShop_GreenScreen/PropShop_Prop_Rock_1",
                                "PropShop_GreenScreen/PropShop_Prop_SeaWeed",
                                "PropShop_GreenScreen/PropShop_Prop_Wagon", "PropShop_GreenScreen/PropShop_Robot",
                                "PropShop_GreenScreen/PropShop_Rover", "PropShop_GreenScreen/PropShop_ScienceBox",
                                "PropShop_GreenScreen/PropShop_ShipWreck",
                                "PropShop_GreenScreen/PropShop_TresureChest",
                                "PropShop_GreenScreen/PropShop_TresureChest_Open",
                                "PropShop_GreenScreen/PropShop_TumbleWeed"
                            }
                        },
                        {
                            "Golf/", new string[]
                            {
                                "Golf Bag", "GolfBall", "GolfBall_Green", "GolfBall_Orange", "GolfBall_Yellow",
                                "GolfClub", "GolfClub_Driver", "GolfClub_Iron"
                            }
                        },
                        {
                            "./", new string[]
                            {
                                "VehicleSpawn_TelephoneBox", "AmericanFridge", "Axe_FireFighter", "Axe_Woodland",
                                "Banana_L", "Banana_M", "Banana_S", "Banana_To_Peel", "Bar Bell",
                                "Barrel", "BaseBallBat_Blue", "BaseBallBat_Green", "BaseBallBat_Yellow", "Bongos",
                                "BobSled", "Cannon_Ball", "Chair_BeachHut", "Floaty_Bed",
                                "Floaty_Crocodile", "Floaty_Duck", "FireExtinguisher", "Garden Gnome", "GarbageBag",
                                "GasBottle", "Jelly", "KnightSword", "LampPost_Dynamic",
                                "Lantern_Blue", "Lawn Flamingo Broken", "Mayor_Key", "Mop", "NewsPaper", "PizzaBox",
                                "PizzaBoxLarge", "Patio Chair", "Patio Table", "Pallet",
                                "Picnicbench", "Plank", "Portable_HosePipe", "PoolNoodle", "Prop_Umbrella_01",
                                "Prop_Umbrella_02", "Prop_Umbrella_03", "RecordPlayer",
                                "RemoteExplosive", "RubberDucky", "RubberDucky_Giant", "RubberDucky_Huge",
                                "RubberDucky_Large", "RubberDucky_Mid", "RubberDucky_Small",
                                "Rubber_Malet", "Small_HokeyPuck", "ShoppingTrolly", "SurfBoard_1", "SurfBoard_2",
                                "Table_BeachHut", "Tennis Ball", "Tennis Racket", "Torch",
                                "Trash Can", "ToolBox", "WickerBasket", "WickerBasket_1", "WorkLight", "WrongSword_1",
                                "WrongSword_2", "WrongSword_3", "WrongSword_4",
                                "WrongSword_5", "WrongSword_6"
                            }
                        }
                    }
                },
                {
                    "Vehicle/", new Dictionary<string, object>
                    {
                        {
                            "Land/Road/", new string[]
                            {
                                "Jungle/Vehicle_CamperVan_Jungle", "Jungle/Vehicle_QuadBike_Jungle",
                                "Trailers/Vehicle_Trailer_Tractor", "Trailers/Vehicle_Trailer_Tractor_Cannon",
                                "Trailers/Vehicle_Trailer_Tractor_Plow", "Trailers/Vehicle_Trailer_Tractor_Seed",
                                "Vehicle_Ambulance", "Vehicle_Bus", "Vehicle_CamperVan", "Vehicle_CaveManCar",
                                "Vehicle_ClownCar", "Vehicle_CombineHarvester", "Vehicle_Convertable",
                                "Vehicle_DeliveryTruck", "Vehicle_Destroyed Car", "Vehicle_Destroyed Car 2 Seater",
                                "Vehicle_Destroyed Truck 2 Seater", "Vehicle_DumpTruck", "Vehicle_Estate",
                                "Vehicle_FBI",
                                "Vehicle_FireTruck", "Vehicle_FlatBedTruck_01", "Vehicle_FlatBedTruck_02",
                                "Vehicle_FlyingCar", "Vehicle_ForkLift", "Vehicle_GarbageTruck", "Vehicle_GolfCart",
                                "Vehicle_HatchBack", "Vehicle_IceCream_Truck", "Vehicle_Jeep",
                                "Vehicle_JellyCar", "Vehicle_LabWaterBottleCar", "Vehicle_Limo", "Vehicle_MonsterTruck",
                                "Vehicle_MotorChopper", "Vehicle_MuscleCar", "Vehicle_OldCar", "Vehicle_Pickup",
                                "Vehicle_PizzaDelivery", "Vehicle_Police Super Car",
                                "Vehicle_PoliceCar", "Vehicle_QuadBike", "Vehicle_RoadRoller", "Vehicle_RocketCar",
                                "Vehicle_Saloon", "Vehicle_Scooter", "Vehicle_Sewer Bike", "Vehicle_Sports_Car",
                                "Vehicle_TalkingSofa", "Vehicle_ThreeWheelCar",
                                "Vehicle_ToxicWaste_Car", "Vehicle_Tractor", "Vehicle_TreeCutter",
                                "Vehicle_WaterBalloonTank", "Vehicle_WreckingBall"
                            }
                        },
                        {
                            "Land/", new string[]
                            {
                                "Vehicle_ToyCar"
                            }
                        },
                        {
                            "Sea/", new string[]
                            {
                                "Vehicle_Sea_Fishing", "Vehicle_Sea_JetSki", "Vehicle_Sea_MotorBoat",
                                "Vehicle_Sea_SmallFishing", "Vehicle_Sea_SpeedBoat", "Vehicle_Sea_SuperYacht",
                                "Vehicle_Sea_TransportBoat", "Vehicle_Sea_TugBoat", "Vehicle_Sea_Yacht"
                            }
                        },
                        {
                            "Air/", new string[]
                            {
                                "Vehicle_Egg UFO", "Vehicle_FighterJet", "Vehicle_FighterPlane", "Vehicle_Glider",
                                "Vehicle_Helicopter", "Vehicle_HelicopterBig", "Vehicle_Helicopter_Police",
                                "Vehicle_HotAirBalloon_01", "Vehicle_HotAirBalloon_02", "Vehicle_LargePlane",
                                "Vehicle_RescueHelicopter", "Vehicle_SeaPlane", "Vehicle_UFO", "Vehicle_WobblyPlane",
                                "Vehicle_YellowPlane", "Vehicle_WeatherDrone"
                            }
                        }
                    }
                },
                {
                    "Holiday/", new Dictionary<string, object>
                    {
                        {
                            "Christmas/", new string[]
                            {
                                "CandyCane", "Snowman", "Snowman Head"
                            }
                        },
                    }
                }
            }
        },
        {
            "Arcade/", new Dictionary<string, object>
            {
                {
                    "Trash Man/Prefabs/Props/", new Dictionary<string, object>
                    {
                        {
                            "Dynamic Objects/", new string[]
                            {
                                "Broken Grand Piano", "TrashBag", "TrashBag Big", "TrashBag Medium"
                            }
                        }
                    }
                },
                {
                    "Sandbox/Prefabs/Props/", new Dictionary<string, object>
                    {
                        {
                            "Props/", new string[]
                            {
                                "DrumStick", "Cow_Dynamic Sandbox"
                            }
                        },
                        {
                            "FoamBlock/", new string[]
                            {
                                "FoamBlock_Bridge", "FoamBlock_Cube", "FoamBlock_Cylinder", "FoamBlock_CylinderSmall",
                                "FoamBlock_Flat", "FoamBlock_Oblong", "FoamBlock_WedgeLarge", "FoamBlock_WedgeSmall",
                                "FoamBlock_HalfCircle"
                            }
                        },
                    }
                },
                {
                    "ArcadeLobby/Prefabs/Props/", new Dictionary<string, object>
                    {
                        {
                            "Arcade/", new string[]
                            {
                                "ArcadeMachines_OutofOrder"
                            }
                        },
                        {
                            "PlayZone/", new string[]
                            {
                                "FoamDuck", "FoamElephant", "FoamTurtle"
                            }
                        },
                        {
                            "RaceTrack/", new string[]
                            {
                                "RaceRoomHockey"
                            }
                        }
                    }
                },
                {
                    "Wobble Run/Prefabs/Props/", new Dictionary<string, object>
                    {
                        {
                            "./", new string[]
                            {
                                "FallingPlatform_Small_Dynamic"
                            }
                        }
                    }
                }
            }
        }
    };*/
}

public class PropSpawnerTab : TabWithIcon
{
    public string SelectedProp
    {
        set
        {
            selectedObject = value;
            spawnBtn.ButtonText.text = $"Spawn Prop ({value})";
        }
    }

    public GameObject InspectedObjectToSpawn
    {
        get
        {
            foreach (var inspector in InspectorManager.Inspectors)
            {
                if (!inspector.IsActive)
                {
                    continue;
                }

                var inspectedObj = inspector.Target;

                if (inspectedObj is not GameObject && inspectedObj is not Component)
                {
                    continue;
                }

                if (inspectedObj is Component component && !component.gameObject.TryGetComponent<HawkNetworkBehaviour>(out _))
                {
                    continue;
                }

                if (inspectedObj is GameObject go && !go.TryGetComponent<HawkNetworkBehaviour>(out _))
                {
                    continue;
                }

                return inspectedObj as GameObject;
            }

            return null;
        }
    }

    private GameObject scrollView;
    private GameObject scrollViewContent;
    private string selectedObject;
    private PropCellHandler cellHandler;
    private ButtonRef spawnBtn;
    private ButtonRef spawnInspectedBtn;
    private ButtonRef inspectBtn;
    private GameObject lastSpawnedProp;

    public PropSpawnerTab(Sprite icon) : base(icon)
    {
        Name = "Prop Spawner";
    }

    public override void ConstructUI(GameObject root)
    {
        base.ConstructUI(root);

        UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", root), 0, 5, 9999, 0);

        var scrollGroup = UIFactory.CreateUIObject("group", root);
        UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(scrollGroup, true, false, true, true);
        UIFactory.SetLayoutElement(scrollGroup, 0, 563, 9999, 0);

        UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", scrollGroup), 5, 0, 0, 9999);

        scrollView = UIFactory.CreateScrollView(scrollGroup, "scroll", out scrollViewContent, out _, HacksUIHelper.BGColor2);
        UIFactory.SetLayoutElement(scrollView, 0, 0, 9999, 9999);
        UIFactory.SetLayoutElement(scrollViewContent, 0, 0, 9999, 9999);

        cellHandler = new(scrollViewContent, this, PropAddressData.AddressTree);
        cellHandler.CreateCells();

        UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", scrollGroup), 5, 0, 0, 9999);
        UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", root), 0, 5, 9999, 0);

        spawnBtn = UIFactory.CreateButton(root, "SpawnBtn", "Spawn Prop", HacksUIHelper.ButtonColor4);
        spawnBtn.Transform.GetComponent<Image>().sprite = HacksUIHelper.RoundedRect;
        spawnBtn.OnClick = () =>
        {
            if(selectedObject != null && selectedObject != "")
            {
                InstantiateSingleProp(selectedObject);
            }
        };
        UIFactory.SetLayoutElement(spawnBtn.GameObject, 0, 32, 9999, 0);

        UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", root), 0, 5, 0, 9999);
            
        inspectBtn = UIFactory.CreateButton(root, "inspect", "Inspect Last Spawned Object (Unity Explorer)", HacksUIHelper.ButtonColor4);
        inspectBtn.Transform.GetComponent<Image>().sprite = HacksUIHelper.RoundedRect;
        inspectBtn.OnClick = () =>
        {
            if (lastSpawnedProp != null)
            {
                InspectorManager.Inspect(lastSpawnedProp);
                UIManager.ShowMenu = true;
            }
        };
        UIFactory.SetLayoutElement(inspectBtn.GameObject, 0, 32, 9999, 0);

        UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", root), 0, 5, 9999, 0);

        spawnInspectedBtn = UIFactory.CreateButton(root, "spawn inspected", "Spawn Inspected Object (Unity Explorer)", HacksUIHelper.ButtonColor4);
        spawnInspectedBtn.Transform.GetComponent<Image>().sprite = HacksUIHelper.RoundedRect;
        spawnInspectedBtn.OnClick = () =>
        {
            if(InspectedObjectToSpawn != null)
            {
                InstantiateSingleProp(InspectedObjectToSpawn.GetComponent<HawkNetworkBehaviour>());
            }
        };
        UIFactory.SetLayoutElement(spawnInspectedBtn.GameObject, 0, 32, 9999, 0);
    }

    private void InstantiateSingleProp(string prop)
    {
        var player = GameInstance.Instance.GetFirstLocalPlayerController();

        if (player == null || prop == "" || prop == null)
        {
            return;
        }

        var character = player.GetPlayerCharacter();
        var pos = character.GetPlayerPosition() + character.GetPlayerForward();

        prop = prop.Replace("./", "");

        TryInstantiatePrefab(prop, pos);
    }

    private void TryInstantiatePrefab(string address, Vector3 position)
    {
        if(!address.EndsWith(".prefab"))
        {
            address += ".prefab";
        }

        if(address.StartsWith("Assets/Content/"))
        {
            address = address.Substring("Assets/Content/".Length);
        }

        NetworkPrefab.SpawnNetworkPrefab(address, (behavior) =>
        {
            if (behavior == null)
            {
                NetworkPrefab.SpawnNetworkPrefab("Assets/Content/" + address, (behavior) =>
                {
                    if (behavior != null)
                    {
                        lastSpawnedProp = behavior.gameObject;
                    }

                }, position);
            }
            else
            {
                lastSpawnedProp = behavior.gameObject;
            }

        }, position, bSendTransform: true);
    }

    private void InstantiateSingleProp(HawkNetworkBehaviour prop)
    {
        var player = GameInstance.Instance.GetFirstLocalPlayerController();

        if (player == null || prop == null)
        {
            return;
        }

        var character = player.GetPlayerCharacter();
        var pos = character.GetPlayerPosition() + character.GetPlayerForward();

        lastSpawnedProp = NetworkPrefab.SpawnNetworkPrefab(prop.gameObject, pos, bSendTransform: true).gameObject;
    }

    public override void RefreshUI() { }

    public override void UpdateUI() { }
}

public class PropCell
{
    private bool enabled;

    public bool Enabled => enabled;
    public RectTransform Rect { get; set; }
    public GameObject UIRoot { get; set; }
    public float DefaultHeight => 32;

    public KeyValuePair<string, object> propDictEntry;
    public string singleProp;
    public PropCellState state;

    public PropSpawnerTab parentTab;
    public PropCellHandler cellHandler;
    public PropCell parentCell;

    private Text text;
    private ButtonRef button;
    private GameObject childGroup;

    public GameObject CreateContent(GameObject parent)
    {
        UIRoot = UIFactory.CreateUIObject("PropCell", parent);
        UIFactory.SetLayoutGroup<VerticalLayoutGroup>(UIRoot, false, false, true, true, 6, 0, 0, 16, 0);
        UIFactory.SetLayoutElement(UIRoot, 0, 32, 9999, 9999);

        Rect = UIRoot.GetComponent<RectTransform>();

        button = UIFactory.CreateButton(UIRoot, "button", "", HacksUIHelper.ButtonColor);
        button.OnClick = OnCellButtonClicked;
        button.Transform.GetComponent<Image>().sprite = HacksUIHelper.RoundedRect;
        UIFactory.SetLayoutElement(button.GameObject, 0, 32, 9999, 0);

        text = button.ButtonText;

        childGroup = UIFactory.CreateUIObject("childGroup", UIRoot);
        UIFactory.SetLayoutGroup<VerticalLayoutGroup>(childGroup, false, false, true, true, 0, 6, 6, 16, 0);
        UIFactory.SetLayoutElement(childGroup, 0, 0, 9999, 9999);

        return UIRoot;
    }

    public void OnCellButtonClicked()
    {
        if (state == PropCellState.String)
        {
            var propAddress = singleProp;
            var currentParent = parentCell;

            while(true)
            {
                if (currentParent == null)
                {
                    break;
                }

                propAddress = currentParent.propDictEntry.Key + propAddress;
                currentParent = currentParent.parentCell;
            }

            parentTab.SelectedProp = propAddress;
        }
        else
        {
            childGroup?.SetActive(!childGroup.activeSelf);
        }
    }

    public void Disable()
    {
        enabled = false;
        UIRoot.SetActive(false);
    }

    public void Enable()
    {
        enabled = true;
        UIRoot.SetActive(true);
        childGroup.SetActive(false);
    }

    public void ConfigureCell(KeyValuePair<string, object> propDictEntry, PropSpawnerTab parentTab, PropCell parentCell)
    {
        this.parentCell = parentCell;
        this.propDictEntry = propDictEntry;
        this.parentTab = parentTab;
        text.text = propDictEntry.Key;

        var propDictEntryValue = propDictEntry.Value;

        if(propDictEntryValue is Dictionary<string, object> propDictEntryDict)
        {
            cellHandler = new(childGroup, parentTab, propDictEntryDict);
            state = PropCellState.DictionaryChildren;
        }
        else if (propDictEntryValue is string[] propDictEntryStrings)
        {
            cellHandler = new(childGroup, parentTab, propDictEntryStrings);
            state = PropCellState.StringChildren;
        }

        cellHandler.parentCell = this;
        cellHandler.CreateCells();
    }

    public void ConfigureCell(string propString, PropSpawnerTab parentTab, PropCell parentCell)
    {
        this.parentCell = parentCell;
        singleProp = propString;
        this.parentTab = parentTab;
        text.text = propString;
        state = PropCellState.String;
    }
}

public class PropCellHandler
{
    public int ItemCount
    {
        get
        {
            if(parentDict != null)
            {
                return parentDict.Count;
            }
            else if(parentStrings != null && parentStrings.Length != 0)
            {
                return parentStrings.Length;
            }

            return 0;
        }
    }

    public PropSpawnerTab parentTab;
    public Dictionary<string, object> parentDict;
    public string[] parentStrings;
    public PropCell parentCell;
    public GameObject parentObject;

    private List<PropCell> cells = new();

    public PropCellHandler(GameObject parentObject, PropSpawnerTab parentTab, Dictionary<string, object> parentDict)
    {
        this.parentTab = parentTab;
        this.parentDict = parentDict;
        this.parentObject = parentObject;
    }

    public PropCellHandler(GameObject parentObject, PropSpawnerTab parentTab, string[] parentStrings)
    {
        this.parentTab = parentTab;
        this.parentStrings = parentStrings;
        this.parentObject = parentObject;
    }

    public void CreateCells()
    {
        var itemCount = ItemCount;

        for(var i = 0; i < itemCount; i++)
        {
            var cell = new PropCell();
            cell.CreateContent(parentObject);
            cells.Add(cell);
            SetCell(cell, i);

            if(i + 1 < itemCount)
            {
                new HacksUIHelper(parentObject).AddSpacer(6);
            }
        }
    }

    public void SetCell(PropCell cell, int index)
    {
        if(parentDict != null)
        {
            if (index < parentDict.Count)
            {
                cell.ConfigureCell(parentDict.ToList()[index], parentTab, parentCell);
                cell.Enable();
            }
            else
            {
                cell.Disable();
            }
        }

        else if(parentStrings != null)
        {
            if (index < parentStrings.Length)
            {
                cell.ConfigureCell(parentStrings[index], parentTab, parentCell);
                cell.Enable();
            }
            else
            {
                cell.Disable();
            }
        }
    }
}

public enum PropCellState
{
    DictionaryChildren,
    StringChildren,
    String
}