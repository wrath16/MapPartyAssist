using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace MapPartyAssist.Helper {
    internal static class AtkNodeHelper {
        internal static IPluginLog? Log;

        internal static unsafe AtkResNode* GetNodeByIDChain(string addon, params uint[] ids) {
            AtkUnitBase* addonNode = AtkStage.GetSingleton()->RaptureAtkUnitManager->GetAddonByName(addon);
            if(addonNode == null || ids.Length <= 0) {
                return null;
            }
            return GetNodeByIDChain(addonNode->RootNode, ids);
        }

        internal static unsafe AtkResNode* GetNodeByIDChain(AtkUnitBase* addon, params uint[] ids) {
            if(addon == null) {
                return null;
            }
            return GetNodeByIDChain(addon->RootNode, ids);
        }

        internal static unsafe AtkResNode* GetNodeByIDChain(AtkResNode* node, params uint[] ids) {
            if(node == null || ids.Length <= 0) {
                return null;
            }

            if(node->NodeID == ids[0]) {
                if(ids.Length == 1) {
                    return node;
                }

                var newList = new List<uint>(ids);
                newList.RemoveAt(0);

                var childNode = node->ChildNode;
                if(childNode != null) {
                    return GetNodeByIDChain(childNode, newList.ToArray());
                } else if((int)node->Type >= 1000) {
                    var componentNode = node->GetAsAtkComponentNode();
                    var component = componentNode->Component;
                    var uldManager = component->UldManager;
                    childNode = uldManager.NodeList[0];
                    if(childNode == null) {
                        return null;
                    } else {
                        return GetNodeByIDChain(childNode, newList.ToArray());
                    }
                } else {
                    return null;
                }
            } else {
                //check siblings
                var sibNode = node->PrevSiblingNode;
                if(sibNode != null) {
                    return GetNodeByIDChain(sibNode, ids);
                } else {
                    return null;
                }
            }
        }

        internal static unsafe void PrintTextNodes(string addon) {
            AtkUnitBase* addonNode = AtkStage.GetSingleton()->RaptureAtkUnitManager->GetAddonByName(addon);
            if(addonNode != null) {
                //Log.Debug($"addon name: {Marshal.PtrToStringUTF8((nint)addonNode->Name)} ptr: {string.Format("0x{0:X8}", new IntPtr(addonNode).ToString())}");
                Log.Debug($"addon name: {Marshal.PtrToStringUTF8((nint)addonNode->Name)} ptr: 0x{new IntPtr(addonNode).ToString("X8")}");
                PrintTextNodes(addonNode->RootNode);
            } else {
                Log.Debug($"{addon} is null!");
            }
        }

        internal static unsafe void PrintTextNodes(AtkUnitBase* addon) {
            PrintTextNodes(addon->RootNode);
        }

        internal static unsafe void PrintTextNodes(AtkResNode* node, bool checkSiblings = true, bool onlyVisible = true) {
            if(node == null) {
                return;
            }
            int type = (int)node->Type;
            int childCount = node->ChildCount;
            var parentNode = node->ParentNode;
            uint parentNodeId = parentNode != null ? parentNode->NodeID : 0;

            string parentNodeIDString = "";
            string parentNodeTypeString = "";
            while(parentNode != null) {
                parentNodeIDString += parentNode->NodeID;
                parentNodeTypeString += parentNode->Type;
                parentNode = parentNode->ParentNode;
                if(parentNode != null) {
                    parentNodeIDString += "<-";
                    parentNodeTypeString += "<-";
                }
            }
            if(type >= 1000) {
                childCount = node->GetAsAtkComponentNode()->Component->UldManager.NodeListCount;
            }

            if(node->Type == NodeType.Text) {
                var tNode = node->GetAsAtkTextNode();
                if(tNode != null) {
                    string nodeText = tNode->NodeText.ToString();
                    if(!nodeText.IsNullOrEmpty() && (node->IsVisible || !onlyVisible)) {
                        Log.Debug(string.Format("Visible: {5,-6} ID: {0,-8} type: {1,-6} childCount: {2,-4} parentID: {3,-25} parentType: {4}", node->NodeID, type, childCount, parentNodeIDString, parentNodeTypeString, node->IsVisible));
                        Log.Debug($"Text: {tNode->NodeText}");
                    }
                }
            }

            //component
            //Window: 1004
            //DropDownList: 1010
            //List: 1019
            //RadioButton: 1021
            //RadioButton: 1013
            if(type >= 1000) {
                var componentNode = node->GetAsAtkComponentNode();
                var component = componentNode->Component;
                var uldManager = component->UldManager;

                if(node->IsVisible || !onlyVisible) {
                    for(int i = 0; i < uldManager.NodeListCount; i++) {
                        var childNode = uldManager.NodeList[i];
                        PrintTextNodes(childNode, false);
                    }
                }
            }

            //check child nodes
            var child = node->ChildNode;
            if(child != null || (!node->IsVisible && onlyVisible)) {
                PrintTextNodes(child, checkSiblings);
            }

            //check sibling nodes
            var sibNode = node->PrevSiblingNode;
            if(sibNode != null && checkSiblings) {
                PrintTextNodes(sibNode, checkSiblings);
            }
        }

        internal static unsafe void PrintAtkValues(AtkUnitBase* node) {
            if(node->AtkValues != null) {
                for(int i = 0; i < node->AtkValuesCount; i++) {
                    string data = ConvertAtkValueToString(node->AtkValues[i]);
                    Log.Debug(string.Format("index: {0,-5} type: {1,-15} data: {2}", i, node->AtkValues[i].Type, data));
                }
            }
        }

        //internal static unsafe AtkValue* ValidatedAtkValue(AtkUnitBase* addon, int index) {
        //    var value = addon->AtkValues[index];
        //    //if(value == 0) {

        //    //}
        //}

        internal static unsafe string ConvertAtkValueToString(AtkValue value) {
            switch(value.Type) {
                case FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int:
                case FFXIVClientStructs.FFXIV.Component.GUI.ValueType.UInt:
                    return value.Int.ToString();
                case FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Bool:
                    return (value.Int != 0).ToString();
                case FFXIVClientStructs.FFXIV.Component.GUI.ValueType.String:
                case FFXIVClientStructs.FFXIV.Component.GUI.ValueType.AllocatedString:
                case FFXIVClientStructs.FFXIV.Component.GUI.ValueType.String8:
                    return Marshal.PtrToStringUTF8((nint)value.String);
                default:
                    break;
            }
            return "";
        }

        internal static unsafe void PrintAtkStringArray() {
            //int index = 0;
            //var stringArray = AtkStage.GetSingleton()->GetStringArrayData()[index];

            int count = 0;
            var stringArray = AtkStage.GetSingleton()->GetStringArrayData()[0];
            while(stringArray != null) {

                int internalCount = 0;
                var internalArray = stringArray->StringArray[0];
                while(internalArray != null) {
                    ////internalArray = stringArray[internalCount];
                    //int secondInternalCount = 0;
                    //var secondInternalArray = internalArray[0];

                    //while(secondInternalArray != null) {
                    //    Log.Debug(ReadString(secondInternalArray));
                    //    //Log.Debug(Marshal.PtrToStringUTF8((nint)secondInternalArray));
                    //    secondInternalCount++;
                    //    secondInternalArray = internalArray[secondInternalCount];
                    //}

                    Log.Debug($"{count} {internalCount}\t{ReadString(internalArray)}");

                    internalCount++;
                    internalArray = stringArray->StringArray[internalCount];
                }
                Log.Debug($"{count} Total strings: {internalCount}");

                count++;
                stringArray = AtkStage.GetSingleton()->GetStringArrayData()[count];
            }

            Log.Debug($"Total AtkStageStringArrays: {count}");

            //for(int i = 0; i < string)

            //while(stringArray != null) {
            //    //stringArray->StringArray;
            //}
        }

        public static unsafe string ReadString(byte* b, int maxLength = 0, bool nullIsEmpty = true) {
            if(b == null) return nullIsEmpty ? string.Empty : null;
            if(maxLength > 0) return Encoding.UTF8.GetString(b, maxLength).Split('\0')[0];
            var l = 0;
            while(b[l] != 0) l++;
            return Encoding.UTF8.GetString(b, l);
        }
    }
}
