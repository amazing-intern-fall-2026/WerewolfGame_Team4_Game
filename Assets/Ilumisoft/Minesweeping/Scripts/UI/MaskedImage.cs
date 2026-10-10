using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Ilumisoft.Minesweeping.UI
{
    public class MaskedImage : Image
    {
        public override Material materialForRendering
        {
            get
            {
                Material material = new Material(base.materialForRendering);
                material.SetInt("_StencilComp", (int)CompareFunction.NotEqual);
                return material;
            }
        }
    }
}