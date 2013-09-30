using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KAS
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class KASAddonEditContainer : MonoBehaviour
    { 
        public void Awake()
        {
            KASAddonControlKey.LoadKeyConfig();
            KASAddonPointer.LoadKeyConfig();
        }
     
        public void Update()
        {
            EditorLogic editor = EditorLogic.fetch;
            if (editor == null) return;
            if (editor.editorScreen != EditorLogic.EditorScreen.Parts) return;

            if (Input.GetKeyDown(KeyCode.Mouse1))
            {
                Part p = KAS_Shared.GetPartUnderCursor();
                if (p)
                {
                    KASModuleContainer containerModule = p.GetComponent<KASModuleContainer>();
                    if (containerModule)
                    {
                        containerModule.EditContents();
                    }
                }            
            }
        }
    }
}
