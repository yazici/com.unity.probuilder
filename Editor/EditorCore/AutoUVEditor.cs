#pragma warning disable 0414

using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.ProBuilder;
using UnityEditor.ProBuilder.UI;

namespace UnityEditor.ProBuilder
{
	/// <summary>
	/// Custom editor for pb_UV type.
	/// </summary>
	class AutoUVEditor
	{
#if !PROTOTYPE
#region MEMBERS

		static ProBuilderEditor editor { get { return ProBuilderEditor.instance; } }

		static AutoUnwrapSettings uv_gui = new AutoUnwrapSettings();		// store GUI changes here, so we may selectively apply them later
		static int textureGroup = -1;

		static List<AutoUnwrapSettings> uv_selection = new List<AutoUnwrapSettings>();
		static Dictionary<string, bool> uv_diff = new Dictionary<string, bool>() {
			{"projectionAxis", false},
			{"useWorldSpace", false},
			{"flipU", false},
			{"flipV", false},
			{"swapUV", false},
			{"fill", false},
			{"scalex", false},
			{"scaley", false},
			{"offsetx", false},
			{"offsety", false},
			{"rotation", false},
			{"anchor", false},
			{"manualUV", false},
			{"textureGroup", false}
		};

		public enum pb_Axis2d {
			XY,
			X,
			Y
		}
#endregion

#region ONGUI

		static Vector2 scrollPos;

		/**
		 * Returns true on GUI change detected.
		 */
		public static bool OnGUI(ProBuilderMesh[] selection, int maxWidth)
		{
			int width = maxWidth - 36;	// scrollbar is 36px

			UpdateDiffDictionary(selection);

			scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

			int tempInt = -1;
			float tempFloat = 0f;
			Vector2 tempVec2 = Vector2.zero;
			bool tempBool = false;

			EditorGUI.BeginChangeCheck();

			/**
			 * Set Tile mode
			 */
			GUILayout.Label("Tiling & Alignment", EditorStyles.boldLabel);

			GUILayout.BeginHorizontal();
				tempInt = (int)uv_gui.fill;
				EditorGUI.showMixedValue = uv_diff["fill"];
				GUILayout.Label("Fill Mode", GUILayout.MaxWidth(80), GUILayout.MinWidth(80));
				uv_gui.fill = (AutoUnwrapSettings.Fill)EditorGUILayout.EnumPopup(uv_gui.fill);
				if(tempInt != (int)uv_gui.fill) SetFill(uv_gui.fill, selection);
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
				bool enabled = GUI.enabled;
				GUI.enabled = !uv_gui.useWorldSpace;
				tempInt = (int) uv_gui.anchor;
				EditorGUI.showMixedValue = uv_diff["anchor"];
				GUILayout.Label("Anchor", GUILayout.MaxWidth(80), GUILayout.MinWidth(80));
				uv_gui.anchor = (AutoUnwrapSettings.Anchor) EditorGUILayout.EnumPopup(uv_gui.anchor);
				if(tempInt != (int)uv_gui.anchor) SetAnchor(uv_gui.anchor, selection);
				GUI.enabled = enabled;
			GUILayout.EndHorizontal();

			UnityEngine.GUI.backgroundColor = PreferenceKeys.proBuilderLightGray;
			UI.EditorGUIUtility.DrawSeparator(1);
			UnityEngine.GUI.backgroundColor = Color.white;

			GUILayout.Label("Transform", EditorStyles.boldLabel);

			/**
			 * Offset
			 */
			EditorGUI.showMixedValue = uv_diff["offsetx"] || uv_diff["offsety"];
			tempVec2 = uv_gui.offset;
			UnityEngine.GUI.SetNextControlName("offset");
			uv_gui.offset = EditorGUILayout.Vector2Field("Offset", uv_gui.offset, GUILayout.MaxWidth(width));
			if(tempVec2.x != uv_gui.offset.x) { SetOffset(uv_gui.offset, pb_Axis2d.X, selection); }
			if(tempVec2.y != uv_gui.offset.y) { SetOffset(uv_gui.offset, pb_Axis2d.Y, selection); }

			/**
			 * Rotation
			 */
			tempFloat = uv_gui.rotation;
			EditorGUI.showMixedValue = uv_diff["rotation"];
			GUILayout.Label(new GUIContent("Rotation", "Rotation around the center of face UV bounds."), GUILayout.MaxWidth(width-64));
			UnityEngine.GUI.SetNextControlName("rotation");
			EditorGUI.BeginChangeCheck();
			tempFloat = EditorGUILayout.Slider(tempFloat, 0f, 360f, GUILayout.MaxWidth(width));
			if(EditorGUI.EndChangeCheck())
				SetRotation(tempFloat, selection);

			/**
			 * Scale
			 */
			EditorGUI.showMixedValue = uv_diff["scalex"] || uv_diff["scaley"];
			tempVec2 = uv_gui.scale;
			UnityEngine.GUI.SetNextControlName("scale");
			EditorGUI.BeginChangeCheck();
			uv_gui.scale = EditorGUILayout.Vector2Field("Tiling", uv_gui.scale, GUILayout.MaxWidth(width));

			if(EditorGUI.EndChangeCheck())
			{
				if(tempVec2.x != uv_gui.scale.x) { SetScale(uv_gui.scale, pb_Axis2d.X, selection); }
				if(tempVec2.y != uv_gui.scale.y) { SetScale(uv_gui.scale, pb_Axis2d.Y, selection); }
			}

			// Draw tiling shortcuts
			GUILayout.BeginHorizontal();
			if( GUILayout.Button(".5", EditorStyles.miniButtonLeft) )	SetScale(Vector2.one * 2f, pb_Axis2d.XY, selection);
			if( GUILayout.Button("1", EditorStyles.miniButtonMid) )		SetScale(Vector2.one, pb_Axis2d.XY, selection);
			if( GUILayout.Button("2", EditorStyles.miniButtonMid) )		SetScale(Vector2.one * .5f, pb_Axis2d.XY, selection);
			if( GUILayout.Button("4", EditorStyles.miniButtonMid) )		SetScale(Vector2.one * .25f, pb_Axis2d.XY, selection);
			if( GUILayout.Button("8", EditorStyles.miniButtonMid) )		SetScale(Vector2.one * .125f, pb_Axis2d.XY, selection);
			if( GUILayout.Button("16", EditorStyles.miniButtonRight) ) 	SetScale(Vector2.one * .0625f, pb_Axis2d.XY, selection);
			GUILayout.EndHorizontal();

			GUILayout.Space(4);

			UnityEngine.GUI.backgroundColor = PreferenceKeys.proBuilderLightGray;
			UI.EditorGUIUtility.DrawSeparator(1);
			UnityEngine.GUI.backgroundColor = Color.white;

			/**
			 * Special
			 */
			GUILayout.Label("Special", EditorStyles.boldLabel);

			tempBool = uv_gui.useWorldSpace;
			EditorGUI.showMixedValue = uv_diff["useWorldSpace"];
			uv_gui.useWorldSpace = EditorGUILayout.Toggle("World Space", uv_gui.useWorldSpace);
			if(uv_gui.useWorldSpace != tempBool) SetUseWorldSpace(uv_gui.useWorldSpace, selection);

			UnityEngine.GUI.backgroundColor = PreferenceKeys.proBuilderLightGray;
			UI.EditorGUIUtility.DrawSeparator(1);
			UnityEngine.GUI.backgroundColor = Color.white;


			// Flip U
			tempBool = uv_gui.flipU;
			EditorGUI.showMixedValue = uv_diff["flipU"];
			uv_gui.flipU = EditorGUILayout.Toggle("Flip U", uv_gui.flipU);
			if(tempBool != uv_gui.flipU) SetFlipU(uv_gui.flipU, selection);

			// Flip V
			tempBool = uv_gui.flipV;
			EditorGUI.showMixedValue = uv_diff["flipV"];
			uv_gui.flipV = EditorGUILayout.Toggle("Flip V", uv_gui.flipV);
			if(tempBool != uv_gui.flipV) SetFlipV(uv_gui.flipV, selection);

			tempBool = uv_gui.swapUV;
			EditorGUI.showMixedValue = uv_diff["swapUV"];
			uv_gui.swapUV = EditorGUILayout.Toggle("Swap U/V", uv_gui.swapUV);
			if(tempBool != uv_gui.swapUV) SetSwapUV(uv_gui.swapUV, selection);

			/**
			 * Texture Groups
			 */
			GUILayout.Label("Texture Groups", EditorStyles.boldLabel);

			tempInt = textureGroup;
			EditorGUI.showMixedValue = uv_diff["textureGroup"];

			UnityEngine.GUI.SetNextControlName("textureGroup");
			textureGroup = UI.EditorGUIUtility.IntFieldConstrained( new GUIContent("Texture Group", "Faces in a texture group will be UV mapped as a group, just as though you had selected these faces and used the \"Planar Project\" action"), textureGroup, width);

			if(tempInt != textureGroup)
			{
				SetTextureGroup(selection, textureGroup);

				foreach(var kvp in editor.SelectedFacesInEditZone)
					kvp.Key.RefreshUV(kvp.Value);

				SceneView.RepaintAll();

				uv_diff["textureGroup"] = false;
			}

			if(GUILayout.Button(new GUIContent("Group Selected Faces", "This sets all selected faces to share a texture group.  What that means is that the UVs on these faces will all be projected as though they are a single plane.  Ideal candidates for texture groups are floors with multiple faces, walls with edge loops, flat surfaces, etc."), GUILayout.MaxWidth(width)))
			{
				for(int i = 0; i < selection.Length; i++)
					TextureGroupSelectedFaces(selection[i]);

				ProBuilderEditor.instance.UpdateSelection();
			}

			if(GUILayout.Button(new GUIContent("Break Selected Groups", "This resets all the selected face Texture Groups."), GUILayout.MaxWidth(width)))
			{
				SetTextureGroup(selection, -1);

				foreach(var kvp in editor.SelectedFacesInEditZone)
				{
					kvp.Key.ToMesh();
					kvp.Key.Refresh();
					kvp.Key.Optimize();
				}

				SceneView.RepaintAll();

				uv_diff["textureGroup"] = false;

				ProBuilderEditor.instance.UpdateSelection();
			}

			/* Select all in current texture group */
			if(GUILayout.Button(new GUIContent("Select Texture Group", "Selects all faces contained in this texture group."), GUILayout.MaxWidth(width)))
			{
				for(int i = 0; i < selection.Length; i++)
					selection[i].SetSelectedFaces( System.Array.FindAll(selection[i].faces, x => x.textureGroup == textureGroup) );

				ProBuilderEditor.instance.UpdateSelection();
			}

			if(GUILayout.Button(new GUIContent("Reset UVs", "Reset UV projection parameters."), GUILayout.MaxWidth(width)))
			{
				UndoUtility.RecordSelection(selection, "Reset UVs");

				for(int i = 0; i < selection.Length; i++)
				{
					foreach(Face face in selection[i].SelectedFaces)
					{
						face.uv = new AutoUnwrapSettings();
					}
				}

				ProBuilderEditor.instance.UpdateSelection();
			}


			UnityEngine.GUI.backgroundColor = PreferenceKeys.proBuilderLightGray;
			UI.EditorGUIUtility.DrawSeparator(1);
			UnityEngine.GUI.backgroundColor = Color.white;

			/**
			 * Clean up
			 */
			GUILayout.EndScrollView();
			EditorGUI.showMixedValue = false;

			return EditorGUI.EndChangeCheck();
		}

		/**
		 * Sets the pb_UV list and diff tables.
		 */
		static void UpdateDiffDictionary(ProBuilderMesh[] selection)
		{
			uv_selection.Clear();

			if(selection == null || selection.Length < 1)
				return;

			uv_selection = selection.SelectMany(x => x.SelectedFaces).Where(x => !x.manualUV).Select(x => x.uv).ToList();

			// Clear values for each iteration
			foreach(string key in uv_diff.Keys.ToList())
				uv_diff[key] = false;

			if(uv_selection.Count < 1) return;

			uv_gui = new AutoUnwrapSettings(uv_selection[0]);

			foreach(AutoUnwrapSettings u in uv_selection)
			{
				// if(u.projectionAxis != uv_gui.projectionAxis)
				// 	uv_diff["projectionAxis"] = true;
				if(u.useWorldSpace != uv_gui.useWorldSpace)
					uv_diff["useWorldSpace"] = true;
				if(u.flipU != uv_gui.flipU)
					uv_diff["flipU"] = true;
				if(u.flipV != uv_gui.flipV)
					uv_diff["flipV"] = true;
				if(u.swapUV != uv_gui.swapUV)
					uv_diff["swapUV"] = true;
				if(u.fill != uv_gui.fill)
					uv_diff["fill"] = true;
				if(u.scale.x != uv_gui.scale.x)
					uv_diff["scalex"] = true;
				if(u.scale.y != uv_gui.scale.y)
					uv_diff["scaley"] = true;
				if(u.offset.x != uv_gui.offset.x)
					uv_diff["offsetx"] = true;
				if(u.offset.y != uv_gui.offset.y)
					uv_diff["offsety"] = true;
				if(u.rotation != uv_gui.rotation)
					uv_diff["rotation"] = true;
				if(u.anchor != uv_gui.anchor)
					uv_diff["anchor"] = true;
			}

			foreach(ProBuilderMesh pb in selection)
			{
				if(uv_diff["manualUV"] && uv_diff["textureGroup"])
					break;

				Face[] selFaces = pb.SelectedFaces;

				if(!uv_diff["manualUV"])
					uv_diff["manualUV"] = System.Array.Exists(selFaces, x => x.manualUV);

				List<int> texGroups = selFaces.Select(x => x.textureGroup).Distinct().ToList();
				textureGroup = texGroups.FirstOrDefault(x => x > -1);

				if(!uv_diff["textureGroup"])
					uv_diff["textureGroup"] = texGroups.Count() > 1;
			}
		}
#endregion

#region MODIFY SINGLE PROPERTIES

		private static void SetFlipU(bool flipU, ProBuilderMesh[] sel)
		{
			UndoUtility.RecordSelection(sel, "Flip U");
			for(int i = 0; i < sel.Length; i++)
			{
				foreach(Face q in sel[i].SelectedFaces) {
					q.uv.flipU = flipU;
				}
			}
		}

		private static void SetFlipV(bool flipV, ProBuilderMesh[] sel)
		{
			UndoUtility.RecordSelection(sel, "Flip V");
			for(int i = 0; i < sel.Length; i++) {
				foreach(Face q in sel[i].SelectedFaces) {
					q.uv.flipV = flipV;
				}
			}
		}

		private static void SetSwapUV(bool swapUV, ProBuilderMesh[] sel)
		{
			UndoUtility.RecordSelection(sel, "Swap U, V");
			for(int i = 0; i < sel.Length; i++) {
				foreach(Face q in sel[i].SelectedFaces) {
					q.uv.swapUV = swapUV;
				}
			}
		}

		private static void SetUseWorldSpace(bool useWorldSpace, ProBuilderMesh[] sel)
		{
			UndoUtility.RecordSelection(sel, "Use World Space UVs");
			for(int i = 0; i < sel.Length; i++) {
				foreach(Face q in sel[i].SelectedFaces) {
					q.uv.useWorldSpace = useWorldSpace;
				}
			}
		}

		private static void SetFill(AutoUnwrapSettings.Fill fill, ProBuilderMesh[] sel)
		{
			UndoUtility.RecordSelection(sel, "Fill UVs");
			for(int i = 0; i < sel.Length; i++)
			{
				foreach(Face q in sel[i].SelectedFaces) {
					q.uv.fill = fill;
				}
			}
		}

		private static void SetAnchor(AutoUnwrapSettings.Anchor anchor, ProBuilderMesh[] sel)
		{
			UndoUtility.RecordSelection(sel, "Set UV Anchor");

			for(int i = 0; i < sel.Length; i++)
			{
				foreach(Face q in sel[i].SelectedFaces)
					q.uv.anchor = anchor;
			}
		}

		private static void SetOffset(Vector2 offset, pb_Axis2d axis, ProBuilderMesh[] sel)
		{
			UndoUtility.RecordSelection(sel, "Offset UVs");

			for(int i = 0; i < sel.Length; i++)
			{
				foreach(Face q in sel[i].SelectedFaces) {
					switch(axis)
					{
						case pb_Axis2d.XY:
							q.uv.offset = offset;
							break;
						case pb_Axis2d.X:
							q.uv.offset = new Vector2(offset.x, q.uv.offset.y);
							break;
						case pb_Axis2d.Y:
							q.uv.offset = new Vector2(q.uv.offset.x, offset.y);
							break;
					}
				}
			}
		}

		private static void SetRotation(float rot, ProBuilderMesh[] sel)
		{
			UndoUtility.RecordSelection(sel, "Rotate UVs");

			for(int i = 0; i < sel.Length; i++)
			{
				foreach(Face q in sel[i].SelectedFaces) {
					q.uv.rotation = rot;
				}
			}
		}

		private static void SetScale(Vector2 scale, pb_Axis2d axis, ProBuilderMesh[] sel)
		{
			UndoUtility.RecordSelection(sel, "Scale UVs");

			for(int i = 0; i < sel.Length; i++)
			{
				foreach(Face q in sel[i].SelectedFaces) {
					switch(axis)
					{
						case pb_Axis2d.XY:
							q.uv.scale = scale;
							break;
						case pb_Axis2d.X:
							q.uv.scale = new Vector2(scale.x, q.uv.scale.y);
							break;
						case pb_Axis2d.Y:
							q.uv.scale = new Vector2(q.uv.scale.x, scale.y);
							break;
					}
				}
			}
		}
#endregion

#region TEXTURE GROUPS

		private static void SetTextureGroup(ProBuilderMesh[] selection, int tex)
		{
			UndoUtility.RecordSelection(selection, "Set Texture Group " + textureGroup);

			foreach(ProBuilderMesh pb in selection)
			{
				if(pb.SelectedFaceCount < 1)
					continue;

				Face[] faces = pb.SelectedFaces;
				AutoUnwrapSettings cuv = faces[0].uv;

				foreach(Face f in faces)
				{
					f.textureGroup = tex;
					f.uv = new AutoUnwrapSettings(cuv);
				}
			}

		}

		private static void TextureGroupSelectedFaces(ProBuilderMesh pb)//, pb_Face face)
		{
			if(pb.SelectedFaceCount < 1) return;

			Face[] faces = pb.SelectedFaces;

			AutoUnwrapSettings cont_uv = faces[0].uv;

			int texGroup = pb.GetUnusedTextureGroup();

			UndoUtility.RecordSelection(pb, "Create Texture Group" + textureGroup);

			foreach(Face f in faces)
			{
				f.uv = new AutoUnwrapSettings(cont_uv);
				f.textureGroup = texGroup;
			}
		}
#endregion
#endif
	}
}
