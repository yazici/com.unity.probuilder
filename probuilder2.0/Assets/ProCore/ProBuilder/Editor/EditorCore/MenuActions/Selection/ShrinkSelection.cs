using UnityEngine;
using UnityEditor;
using ProBuilder2.Common;
using ProBuilder2.EditorCommon;
using ProBuilder2.Interface;

namespace ProBuilder2.Actions
{
	public class ShrinkSelection : pb_MenuAction
	{
		public override pb_IconGroup group { get { return pb_IconGroup.Selection; } }
		public override Texture2D icon { get { return pb_IconUtility.GetIcon("Selection_Shrink"); } }
		public override pb_TooltipContent tooltip { get { return _tooltip; } }

		static readonly pb_TooltipContent _tooltip = new pb_TooltipContent
		(
			"Shrink Selection",
			@"Does the opposite of Grow.  This removes the elements on the perimeter of the current selection.

<b>Shortcut</b>: <i>Shift + Alt + G</i>"
		);

		public override bool IsEnabled()
		{
			return 	pb_Editor.instance != null &&
					pb_Menu_Commands.VerifyShrinkSelection(selection);
		}

		public override pb_ActionResult DoAction()
		{
			return pb_Menu_Commands.MenuShrinkSelection(selection);
		}
	}
}
