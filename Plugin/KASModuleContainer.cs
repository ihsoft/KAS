using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using UnityEngine;

namespace KAS
{
    public class KASModuleContainer : PartModule
    {
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            KIS.ModuleKISInventory moduleInventory = this.part.GetComponent<KIS.ModuleKISInventory>();
            if (!moduleInventory) return;

            foreach (ConfigNode cn in node.nodes)
            {
                if (cn.name == "CONTENT")
                {
                    string AvPartName = cn.GetValue("name") ?? "null";
                    AvailablePart availablePart = PartLoader.getPartInfoByName(AvPartName);
                    if (availablePart != null)
                    {
                        int qty = int.Parse(cn.GetValue("qty"));
                        moduleInventory.AddItem(availablePart.partPrefab, qty);
                    }
                }
                if (cn.name == "CONTENT_PART")
                {
                    string AvPartName = cn.GetValue("name") ?? "null";
                    AvailablePart availablePart = PartLoader.getPartInfoByName(AvPartName);
                    if (availablePart != null)
                    {
                        ConfigNode itemNode = new ConfigNode();
                        cn.CopyTo(itemNode.AddNode("PART"));
                        moduleInventory.AddItem(availablePart, itemNode, 1);
                    }
                }
            }
        }

     
    }
}
