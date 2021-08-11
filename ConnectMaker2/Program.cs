using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
	partial class Program : MyGridProgram
	{
        public PistonStates state = PistonStates.Start;
        public float pistonTopMaxRetraction = 0;
        public float pistonTopMaxExtension = 0;
        public float pistonBotMaxRetraction = 0;
        public float pistonBotMaxExtension = 0;
        public bool topLockConnected;
        public bool botLockConnected;

        public enum PistonStates
        {
            Start,
            WaitForBottomExtension,
            BottomExtensionDone,
            WaitForTopExtension,
            TopExtensionDone,
            WaitForBottomRetraction,
            BottomRetractionDone,
            WaitForTopRetraction,
            TopRetractionDone,
            Done
        }

        public class RequiredItem
        {
            public string Type;
            public double Quantity;
            public double MinAmount;
            public bool Enough;

            public RequiredItem(string type, double quantity, int minAmount, bool enough)
            {
                Type = type;
                Quantity = quantity;
                MinAmount = minAmount;
                Enough = enough;
            }
        }

        public RequiredItem[] requiredItems =
        {
            new RequiredItem("SteelPlate", 0,3500, false),
            new RequiredItem("Construction",0,500, false),
            new RequiredItem("InteriorPlate",0,400, false),
            new RequiredItem("SmallTube",0,600, false),
            new RequiredItem("LargeTube",0,100,false),
            new RequiredItem("Motor",0,200,false),
            new RequiredItem("Computer",0,100,false)
        };

		public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Main(string argument, UpdateType updateSource)
        {
			

            Echo(Storage);
            //Update State to match storage
            state = RetrieveState();


            //Get all relevant blocks
        IMyTextSurface lcdscreen = GridTerminalSystem.GetBlockWithName("LCDTest") as IMyTextSurface;

            List<IMyPistonBase> pistonsTop = new List<IMyPistonBase>();
            List<IMyPistonBase> pistonsBot = new List<IMyPistonBase>();
            List<IMyShipConnector> connectorsTop = new List<IMyShipConnector>();
            List<IMyShipConnector> connectorsBot = new List<IMyShipConnector>();
            List<IMyShipMergeBlock> mergeBlocks = new List<IMyShipMergeBlock>();
            List<IMyAssembler> assemblers = new List<IMyAssembler>();

            GetPistons(pistonsTop, "top");
            GetPistons(pistonsBot, "bot");

            GetConnectors(connectorsTop, "top");
            GetConnectors(connectorsBot, "bot");

            GetAssembler(assemblers);

            GetMergeBlocks(mergeBlocks);

            

            //Init Welders

            //Extend phase

            switch (state)
            {
                case PistonStates.Start:
                    PrintToScreen(lcdscreen, Storage);
                    GetInventory();

                    bool go = true;
                    foreach (RequiredItem item in requiredItems)
                    {
                        if (!item.Enough)
                        {
                            Echo("Missing:" + item.Type + item.Quantity.ToString() + " out of " + item.MinAmount.ToString());
                            go = false;
                        }
                    }

                    if (!go)
                    {
                        Echo("Not enough materials! Cancelling operation.");
                        break;
                    }
                    else
                    {
                        if (IsBotConnectorsConnected(connectorsBot))
                        {
                            if (IsMergeBlocksConnected(mergeBlocks))
                            {
                                if (!UnlockConnectors(connectorsTop, "top"))
                                {
                                    ExtendPiston(pistonsBot, "bot");
                                    break;
                                }
                                else
                                {
                                    break;
                                }
                            }
                            else
                            {
                                Echo("Merge Blocks unconnected! Cancelling operation.");
                            }
                        }
                        else
                        {
                            Echo("Bot Connectors unconnected! Cancelling operation.");
                        }
                        break;
                    }

                case PistonStates.WaitForBottomExtension:
                    PrintToScreen(lcdscreen, Storage);

                    WaitForExtension(pistonsBot, "bot", pistonBotMaxExtension);
                    break;

                case PistonStates.BottomExtensionDone:
                    PrintToScreen(lcdscreen, Storage);

                    ExtendPiston(pistonsTop, "top");

                    break;

                case PistonStates.WaitForTopExtension:
                    PrintToScreen(lcdscreen, Storage);

                    WaitForExtension(pistonsTop, "top", pistonTopMaxExtension);

                    break;

                case PistonStates.TopExtensionDone:
                    PrintToScreen(lcdscreen, Storage);

                    if (LockConnectors(connectorsTop, "top"))
                    {
                        if (!UnlockMergeBlocks(mergeBlocks))
                        {
                            if (!UnlockConnectors(connectorsBot, "bot"))
                            {
                                RetractPiston(pistonsBot, "bot");
                            }
                            else
                            {
                                Echo("cant unlock bot connectors");
                            }
                        }
                        else
                        {
                            Echo("cant unlock merge blocks");
                        }
                    }
                    else
                    {
                        Echo("cant lock top connectors");
                    }

                    break;

                case PistonStates.WaitForBottomRetraction:
                    PrintToScreen(lcdscreen, Storage);

                    WaitForRetraction(pistonsBot, "bot", pistonBotMaxRetraction);

                    break;

                case PistonStates.BottomRetractionDone:
                    PrintToScreen(lcdscreen, Storage);

                    RetractPiston(pistonsTop, "top");
                    break;

                case PistonStates.WaitForTopRetraction:
                    PrintToScreen(lcdscreen, Storage);

                    WaitForRetraction(pistonsTop, "top", pistonTopMaxRetraction);
                    break;

                case PistonStates.TopRetractionDone:
                    PrintToScreen(lcdscreen, Storage);

                    if (LockMergeBlocks(mergeBlocks))
                    {
                        if (LockConnectors(connectorsBot, "bot"))
                        {
                            StoreState(PistonStates.Done);
                        }
                        else
                        {
                            Echo("cant lock bot connectors!");
                        }
                    }
                    else
                    {
                        Echo("cant lock mergeblocks!");
                    }

                    break;
                case PistonStates.Done:
                    PrintToScreen(lcdscreen, Storage);
                    AddToAssemblyQueue(assemblers);

                    StoreState(PistonStates.Start);

                    break;
                default:

                    break;
            }
        }

        List<IMyPistonBase> GetPistons(List<IMyPistonBase> pistonList, string var)
        {
            List<IMyTerminalBlock> pistons = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyPistonBase>(pistons);

            foreach (IMyTerminalBlock block in pistons)
            {
                if (block is IMyPistonBase)
                {
                    IMyPistonBase piston = block as IMyPistonBase;
                    if (var == "top")
                    {
                        if (piston.CustomName == "PistonT")
                        {
                            pistonList.Add(piston);
                            pistonTopMaxExtension = piston.MaxLimit;
                            pistonTopMaxRetraction = piston.MinLimit;
                        }
                    }
                    else if (var == "bot")
                    {
                        if (piston.CustomName == "PistonB")
                        {
                            pistonList.Add(piston);
                            pistonBotMaxExtension = piston.MaxLimit;
                            pistonBotMaxRetraction = piston.MinLimit;
                        }
                    }
                }
            }
            return pistonList;
        }

        void GetConnectors(List<IMyShipConnector> connectorList, string var)
        {
            List<IMyTerminalBlock> connectors = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(connectors);

            foreach (IMyTerminalBlock block in connectors)
            {
                if (block is IMyShipConnector)
                {
                    IMyShipConnector connector = block as IMyShipConnector;
                    if (var == "top")
                    {
                        if (connector.CustomName == "ConnectorT")
                        {
                            connectorList.Add(connector);
                            Echo("Connector Added");
                        }
                    }
                    else if (var == "bot")
                    {
                        if (connector.CustomName == "ConnectorB")
                        {
                            connectorList.Add(connector);
                            Echo("Connector Added");
                        }
                    }
                }

            }
        }
        void GetMergeBlocks(List<IMyShipMergeBlock> mergeList)
        {
            List<IMyTerminalBlock> mergeblocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyShipMergeBlock>(mergeblocks);

            foreach (IMyTerminalBlock block in mergeblocks)
            {
                if (block is IMyShipMergeBlock)
                {
                    IMyShipMergeBlock mergeblock = block as IMyShipMergeBlock;
                    if (mergeblock.CustomName == "MergeB")
                    {
                        mergeList.Add(mergeblock);
                        Echo("Mergeblock Added");
                    }
                }
            }
        }

        void GetAssembler(List<IMyAssembler> assemblerList)
        {
            List<IMyTerminalBlock> assemblers = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyAssembler>(assemblers);

            foreach (IMyTerminalBlock block in assemblers)
            {
                if (block is IMyAssembler)
                {
                    IMyAssembler assembler = block as IMyAssembler;
                    if (assembler.CustomName == "AssemblerElevator")
                    {
                        assemblerList.Add(assembler);
                        Echo("Assembler Added");
                    }
                }
            }
        }

        void GetInventory()
        {
            List<IMyTerminalBlock> containers = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(containers);

            foreach (RequiredItem item in requiredItems)
            {
                item.Quantity = 0;

                foreach (IMyTerminalBlock block in containers)
                {
                    if (block is IMyCargoContainer)
                    {
                        IMyInventory inv = (IMyInventory)block.GetInventory(0);
                        for (int i = 0; i < inv.ItemCount; i++)
                        {
                            MyInventoryItem item1 = (MyInventoryItem) inv.GetItemAt(i);
                            if (item1 != null)
                            {
                                if (item1.Type.SubtypeId.ToString() == item.Type)
                                {
                                    item.Quantity += (double) item1.Amount;
                                }
                            }
                        }
                    }
                }
                if (item.Quantity > item.MinAmount)
                {
                    Echo(item.Type + item.Quantity.ToString() + " true");
                    item.Enough = true;
                }
                else
                {
                    Echo(item.Type + item.Quantity.ToString() + " false");
                    item.Enough = false;
                }
            }
        }

        void AddToAssemblyQueue(List<IMyAssembler> assemblers)
        {
            foreach (IMyAssembler assembler in assemblers)
            {
                foreach (RequiredItem item in requiredItems)
                {
                    MyDefinitionId id = new MyDefinitionId();

                    if (item.Type == "Construction" || item.Type == "Motor" || item.Type == "Computer")
                    {
                        MyDefinitionId.TryParse("MyObjectBuilder_BlueprintDefinition/" + item.Type + "Component", out id);
                    }
                    else
                    {
                        MyDefinitionId.TryParse("MyObjectBuilder_BlueprintDefinition/" + item.Type, out id);
                    }

                    Echo(id.SubtypeName + id.TypeId);

                    assembler.AddQueueItem(id,item.MinAmount);

                    Echo("added " + item.Type +" to queue from projector!");
                }
            }
        }


        bool LockConnectors(List<IMyShipConnector> connectors, string var)
        {
            bool locked = false;

            foreach (IMyShipConnector connector in connectors)
            {
                if (connector.Status.ToString() == "Connectable" || connector.Status.ToString() == "Connected")
                {
                    connector.Connect();

                    if (connector.Status.ToString() == "Connected")
                    {
                        locked = true;
                    }
                    else
                    {
                        locked = false;
                        Echo("cannot acquire lock on " + var + " connector");
                        return locked;
                    }
                }
                else
                {
                    locked = false;
                    Echo("cannot acquire lock on " + var + " connector");
                    return locked;
                }
            }
            return locked;
        }

        bool UnlockConnectors(List<IMyShipConnector> connectors, string var)
        {
            bool locked = true;

            foreach (IMyShipConnector connector in connectors)
            {
                if (connector.Status.ToString() == "Connected")
                {
                    connector.Disconnect();

                    if (connector.Status.ToString() != "Connected")
                    {
                        locked = false;
                    }
                    else
                    {
                        Echo("cannot unlock " + var + "for some reason");
                        return true;
                    }
                }
                else
                {
                    locked = false;
                }
            }
            return locked;
        }

        bool LockMergeBlocks(List<IMyShipMergeBlock> mergeBlocks)
        {
            bool locked = false;

            foreach (IMyShipMergeBlock block in mergeBlocks)
            {
                if (!block.IsConnected || !block.Enabled)
                {
                    block.Enabled = true;
                    if (!block.IsConnected)
                    {
                        return false;
                    }
                    else
                    {
                        locked = true;
                    }
                }
                else
                {
                    locked = true;
                }
            }
            return locked;
        }

        bool UnlockMergeBlocks(List<IMyShipMergeBlock> mergeBlocks)
        {
            bool locked = true;

            foreach (IMyShipMergeBlock block in mergeBlocks)
            {
                if (block.IsConnected)
                {
                    block.Enabled = false;
                    if (block.IsConnected && block.Enabled)
                    {
                        Echo("Merge block still locked" + block.IsConnected.ToString());
                        return true;
                    }
                    else
                    {
                        locked = false;
                    }
                }
                else
                {
                    locked = false;
                }
            }

            return locked;
        }


        bool IsTopConnectorsConnected(List<IMyShipConnector> connectors)
        {
            if (connectors[0].Status.ToString() == "Connected" && connectors[1].Status.ToString() == "Connected")
            {
                return true;
            }
            else if (connectors[0].Status.ToString() != "Connected" && connectors[1].Status.ToString() != "Connected")
            {
                return false;
            }
            else
            {
                return false;
            }

        }

        bool IsBotConnectorsConnected(List<IMyShipConnector> connectors)
        {
            Echo("1: " + connectors[0].Status.ToString());
            Echo("2: " + connectors[1].Status.ToString());
            if (connectors[0].Status.ToString() == "Connected" && connectors[1].Status.ToString() == "Connected")
            {

                return true;
            }
            else if (connectors[0].Status.ToString() != "Connected" && connectors[1].Status.ToString() != "Connected")
            {
                return false;
            }
            else
            {
                return false;
            }
        }

        bool IsMergeBlocksConnected(List<IMyShipMergeBlock> mergeBlocks)
        {
            if (mergeBlocks[0].IsConnected && mergeBlocks[1].IsConnected)
            {
                return true;
            }
            else if (!mergeBlocks[0].IsConnected && !mergeBlocks[1].IsConnected)
            {
                return false;
            }
            else
            {
                return false;
            }

        }
        void ExtendPiston(List<IMyPistonBase> pistons, string var)
        {
            foreach (IMyPistonBase piston in pistons)
            {
                piston.Extend();
                if (var == "bot")
                {
                    StoreState(PistonStates.WaitForBottomExtension);
                }
                else if (var == "top")
                {
                    StoreState(PistonStates.WaitForTopExtension);
                }
            }
        }

        void WaitForExtension(List<IMyPistonBase> pistons, string var, float max)
        {
            if (pistons[0].CurrentPosition == max && pistons[1].CurrentPosition == max)
            {
                if (var == "top")
                {
                    StoreState(PistonStates.TopExtensionDone);
                }
                else if (var == "bot")
                {
                    StoreState(PistonStates.BottomExtensionDone);
                }
            }
            else
            {
                if (var == "top")
                {
                    StoreState(PistonStates.WaitForTopExtension);
                }
                else if (var == "bot")
                {
                    StoreState(PistonStates.WaitForBottomExtension);
                }
            }
        }

        void RetractPiston(List<IMyPistonBase> pistons, string var)
        {
            foreach (IMyPistonBase piston in pistons)
            {
                piston.Retract();
                if (var == "bot")
                {
                    StoreState(PistonStates.WaitForBottomRetraction);
                }
                else if (var == "top")
                {
                    StoreState(PistonStates.WaitForTopRetraction);
                }
            }
        }

        void WaitForRetraction(List<IMyPistonBase> pistons, string var, float max)
        {
            if (pistons[0].CurrentPosition == max && pistons[1].CurrentPosition == max)
            {
                if (var == "top")
                {
                    StoreState(PistonStates.TopRetractionDone);
                }
                else if (var == "bot")
                {
                    StoreState(PistonStates.BottomRetractionDone);
                }
            }
            else
            {
                if (var == "top")
                {
                    StoreState(PistonStates.WaitForTopRetraction);
                }
                else if (var == "bot")
                {
                    StoreState(PistonStates.WaitForBottomRetraction);
                }
            }
        }


        void StoreState(PistonStates state)
        {
            Storage = state.ToString();
        }

        PistonStates RetrieveState()
        {
            PistonStates st = PistonStates.Start;

            foreach (int i in Enum.GetValues(typeof(PistonStates)))
            {
                if (Storage == Enum.GetNames(typeof(PistonStates))[i])
                {
                    st = (PistonStates)i;
                    break;
                }
            }

            //LCD.TEXT = st.ToString();

            return st;
        }

        void PrintToScreen(IMyTextSurface debug, string text)
        {
            debug.ContentType = ContentType.SCRIPT;
            debug.FontSize = 2;
            using (var frame = debug.DrawFrame())
            {
                MySprite sprite = MySprite.CreateText(text, "debug", new Color(1f), 2f, TextAlignment.CENTER);

                frame.Add(sprite);
            }
        }
    }
}
