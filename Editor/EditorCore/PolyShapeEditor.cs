using UnityEngine;
using UnityEditor;
using ProBuilder.MeshOperations;
using System.Collections.Generic;
using UnityEngine.ProBuilder;
using UnityEditor.ProBuilder.UI;
using RaycastHit = UnityEngine.ProBuilder.RaycastHit;

namespace UnityEditor.ProBuilder
{
	[CustomEditor(typeof(PolyShape))]
	class PolyShapeEditor : Editor
	{
		static Color HANDLE_COLOR = new Color(.8f, .8f, .8f, 1f);
		static Color HANDLE_GREEN = new Color(.01f, .9f, .3f, 1f);
		static Color SELECTED_COLOR = new Color(.01f, .8f, .98f, 1f);

		static readonly Vector3 SNAP_MASK = new Vector3(1f, 0f, 1f);

		Material m_LineMaterial;
		Mesh m_LineMesh = null;
		Plane m_Plane = new Plane(Vector3.up, Vector3.zero);
		bool m_PlacingPoint = false;
		int m_SelectedIndex = -2;
		float m_DistanceFromHeightHandle;
		static float m_HeightMouseOffset;
		bool m_NextMouseUpAdvancesMode = false;
		List<GameObject> m_IgnorePick = new List<GameObject>();
		bool m_IsModifyingVertices = false;

		PolyShape polygon
		{
			get { return target as PolyShape; }
		}

		Material CreateHighlightLineMaterial()
		{
			Material mat = new Material(Shader.Find("Hidden/ProBuilder/ScrollHighlight"));
			mat.SetColor("_Highlight", new Color(0f, 200f / 255f, 170f / 200f, 1f));
			mat.SetColor("_Base", new Color(0f, 136f / 255f, 1f, 1f));
			return mat;
		}

		void OnEnable()
		{
			if (polygon == null)
			{
				DestroyImmediate(this);
				return;
			}

			ProBuilderEditor.AddOnEditLevelChangedListener(OnEditLevelChange);
			m_LineMesh = new Mesh();
			m_LineMaterial = CreateHighlightLineMaterial();
			Undo.undoRedoPerformed += UndoRedoPerformed;
			DrawPolyLine(polygon.points);
			EditorApplication.update += Update;

			PolyShape.PolyEditMode mode = polygon.polyEditMode;
			polygon.polyEditMode = PolyShape.PolyEditMode.None;
			SetPolyEditMode(mode);
		}

		void OnDisable()
		{
			ProBuilderEditor.RemoveOnEditLevelChangedListener(OnEditLevelChange);
			GameObject.DestroyImmediate(m_LineMesh);
			GameObject.DestroyImmediate(m_LineMaterial);
			EditorApplication.update -= Update;
			Undo.undoRedoPerformed -= UndoRedoPerformed;
		}

		public override void OnInspectorGUI()
		{
			switch (polygon.polyEditMode)
			{
				case PolyShape.PolyEditMode.None:
				{
					if (GUILayout.Button("Edit Poly Shape"))
						SetPolyEditMode(PolyShape.PolyEditMode.Edit);

					EditorGUILayout.HelpBox(
						"Editing a poly shape will erase any modifications made to the mesh!\n\nIf you accidentally enter Edit Mode you can Undo to get your changes back.",
						MessageType.Warning);

					break;
				}

				case PolyShape.PolyEditMode.Path:
				{
					EditorGUILayout.HelpBox("\nClick To Add Points\n\nPress 'Enter' or 'Space' to Set Height\n", MessageType.Info);
					break;
				}

				case PolyShape.PolyEditMode.Height:
				{
					EditorGUILayout.HelpBox("\nMove Mouse to Set Height\n\nPress 'Enter' or 'Space' to Finalize\n", MessageType.Info);
					break;
				}

				case PolyShape.PolyEditMode.Edit:
				{
					if (GUILayout.Button("Editing Poly Shape", UI.EditorGUIUtility.GetActiveStyle("Button")))
						SetPolyEditMode(PolyShape.PolyEditMode.None);
					break;
				}

			}

			EditorGUI.BeginChangeCheck();

			float extrude = polygon.extrude;
			extrude = EditorGUILayout.FloatField("Extrusion", extrude);

			bool flipNormals = polygon.flipNormals;
			flipNormals = EditorGUILayout.Toggle("Flip Normals", flipNormals);

			if (EditorGUI.EndChangeCheck())
			{
				if (polygon.polyEditMode == PolyShape.PolyEditMode.None)
				{
					if (ProBuilderEditor.instance != null)
						ProBuilderEditor.instance.ClearElementSelection();

					UndoUtility.RecordObject(polygon, "Change Polygon Shape Settings");
					UndoUtility.RecordObject(polygon.mesh, "Change Polygon Shape Settings");
				}
				else
				{
					UndoUtility.RecordObject(polygon, "Change Polygon Shape Settings");
				}

				polygon.extrude = extrude;
				polygon.flipNormals = flipNormals;

				RebuildPolyShapeMesh(polygon);
			}

			// GUILayout.Label("selected : " + m_SelectedIndex);
		}

		void Update()
		{
			if (polygon != null && polygon.polyEditMode == PolyShape.PolyEditMode.Path && m_LineMaterial != null)
				m_LineMaterial.SetFloat("_EditorTime", (float) EditorApplication.timeSinceStartup);
		}

		void SetPolyEditMode(PolyShape.PolyEditMode mode)
		{
			PolyShape.PolyEditMode old = polygon.polyEditMode;

			if (mode != old)
			{
				// Clear the control always
				GUIUtility.hotControl = 0;

				// Entering edit mode after the shape has been finalized once before, which means
				// possibly reverting manual changes.  Store undo state so that if this was
				// not intentional user can revert.
				if (polygon.polyEditMode == PolyShape.PolyEditMode.None && polygon.points.Count > 2)
				{
					if (ProBuilderEditor.instance != null)
						ProBuilderEditor.instance.ClearElementSelection();

					UndoUtility.RecordObject(polygon, "Edit Polygon Shape");
					UndoUtility.RecordObject(polygon.mesh, "Edit Polygon Shape");
				}

				polygon.polyEditMode = mode;

				if (ProBuilderEditor.instance != null)
				{
					if (polygon.polyEditMode == PolyShape.PolyEditMode.None)
						ProBuilderEditor.instance.PopEditLevel();
					else
						ProBuilderEditor.instance.SetEditLevel(EditLevel.Plugin);
				}

				if (polygon.polyEditMode != PolyShape.PolyEditMode.None)
					Tools.current = Tool.None;

				// If coming from Path -> Height set the mouse / origin offset
				if (old == PolyShape.PolyEditMode.Path && mode == PolyShape.PolyEditMode.Height && Event.current != null)
				{
					Vector3 up = polygon.transform.up;
					Vector3 origin = polygon.transform.TransformPoint(ProBuilderMath.Average(polygon.points));
					Ray r = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
					Vector3 p = ProBuilderMath.GetNearestPointRayRay(origin, up, r.origin, r.direction);
					m_HeightMouseOffset = polygon.extrude -
					                      ProGridsInterface.ProGridsSnap(
						                      Vector3.Distance(origin, p) * Mathf.Sign(Vector3.Dot(p - origin, up)));
				}

				RebuildPolyShapeMesh(polygon);
			}
		}

		void SetPlane(Vector2 mousePosition)
		{
			GameObject go = null;
			m_IgnorePick.Clear();

			do
			{
				if (go != null)
					m_IgnorePick.Add(go);

				go = HandleUtility.PickGameObject(mousePosition, false, m_IgnorePick.ToArray());
			} while (go != null && go.GetComponent<MeshFilter>() == null);

			if (go != null)
			{
				Mesh m = go.GetComponent<MeshFilter>().sharedMesh;

				if (m != null)
				{
					RaycastHit hit;

					if (UnityEngine.ProBuilder.HandleUtility.WorldRaycast(HandleUtility.GUIPointToWorldRay(mousePosition),
						go.transform,
						m.vertices,
						m.triangles,
						out hit))
					{
						polygon.transform.rotation = Quaternion.LookRotation(go.transform.TransformDirection(hit.normal).normalized) *
						                             Quaternion.Euler(new Vector3(90f, 0f, 0f));
						Vector3 hitPointWorld = go.transform.TransformPoint(hit.point);

						// if hit point on plane is cardinal axis and on grid, snap to grid.
						if (!ProBuilderMath.IsCardinalAxis(polygon.transform.up))
						{
							polygon.isOnGrid = false;
						}
						else
						{
							const float epsilon = .00001f;
							float snapVal = Mathf.Abs(ProGridsInterface.SnapValue());
							float rem = Mathf.Abs(snapVal - (Vector3.Scale(polygon.transform.up, hitPointWorld).magnitude % snapVal));
							polygon.isOnGrid = (rem < epsilon || Mathf.Abs(snapVal - rem) < epsilon);
						}

						polygon.transform.position =
							polygon.isOnGrid ? ProGridsInterface.ProGridsSnap(hitPointWorld, Vector3.one) : hitPointWorld;

						return;
					}
				}
			}

			// No mesh in the way, set the plane based on camera
			SceneView sceneView = SceneView.lastActiveSceneView;
			float cam_x = Vector3.Dot(sceneView.camera.transform.forward, Vector3.right);
			float cam_y = Vector3.Dot(sceneView.camera.transform.position - sceneView.pivot.normalized, Vector3.up);
			float cam_z = Vector3.Dot(sceneView.camera.transform.forward, Vector3.forward);

			ProjectionAxis axis = ProjectionAxis.Y;

			if (Mathf.Abs(cam_x) > .98f)
				axis = ProjectionAxis.X;
			else if (Mathf.Abs(cam_z) > .98f)
				axis = ProjectionAxis.Z;

			polygon.transform.position = ProGridsInterface.ProGridsSnap(polygon.transform.position);

			switch (axis)
			{
				case ProjectionAxis.X:
					polygon.transform.rotation = Quaternion.Euler(new Vector3(0f, 0f, 90f * Mathf.Sign(cam_x)));
					break;

				case ProjectionAxis.Y:
					polygon.transform.rotation = Quaternion.Euler(new Vector3(cam_y < 0f ? 180f : 0f, 0f, 0f));
					break;

				case ProjectionAxis.Z:
					polygon.transform.rotation = Quaternion.Euler(new Vector3(-90f * Mathf.Sign(cam_z), 0f, 0f));
					break;
			}
		}

		// Update the pb_Object with the new coordinates. Returns true if mesh successfully triangulated, false if not.
		void RebuildPolyShapeMesh(bool vertexCountChanged = false)
		{
			// If Undo is called immediately after creation this situation can occur
			if (polygon == null)
				return;

			DrawPolyLine(polygon.points);

			if(polygon.polyEditMode == PolyShape.PolyEditMode.Path || polygon.CreateShapeFromPolygon().status != Status.Success)
			{
				ProBuilderEditor.Refresh();
				return;
			}

			if(vertexCountChanged)
				polygon.mesh.Optimize();

			if(ProBuilderEditor.instance != null)
			{
				if(!vertexCountChanged)
					ProBuilderEditor.instance.Internal_UpdateSelectionFast();
				else
					ProBuilderEditor.Refresh();
			}
		}

		void OnSceneGUI()
		{
			if(polygon == null || (polygon.polyEditMode == PolyShape.PolyEditMode.None) || Tools.current != Tool.None)
			{
				polygon.polyEditMode = PolyShape.PolyEditMode.None;
				return;
			}

			if(m_LineMaterial != null)
			{
				m_LineMaterial.SetPass(0);
				Graphics.DrawMeshNow(m_LineMesh, polygon.transform.localToWorldMatrix, 0);
			}

			Event evt = Event.current;

			// used when finishing a loop by clicking the first created point
			if(m_NextMouseUpAdvancesMode && evt.type == EventType.MouseUp)
			{
				evt.Use();

				m_NextMouseUpAdvancesMode = false;

				if( SceneCameraIsAlignedWithPolyUp() )
					SetPolyEditMode(PolyShape.PolyEditMode.Edit);
				else
					SetPolyEditMode(PolyShape.PolyEditMode.Height);
			}

			if(	m_IsModifyingVertices && (
				evt.type == EventType.MouseUp ||
				evt.type == EventType.Ignore ||
				evt.type == EventType.KeyDown ||
				evt.type == EventType.KeyUp ))
			{
				OnFinishVertexMovement();
			}

			DoExistingPointsGUI();

			if(evt.type == EventType.KeyDown)
				HandleKeyEvent(evt.keyCode, evt.modifiers);

			if( EditorHandleUtility.SceneViewInUse(evt) )
				return;

			int controlID = GUIUtility.GetControlID(FocusType.Passive);

			HandleUtility.AddDefaultControl(controlID);

			DoPointPlacement( HandleUtility.GUIPointToWorldRay(evt.mousePosition) );
		}

		void DoPointPlacement(Ray ray)
		{
			Event evt = Event.current;
			EventType eventType = evt.type;

			if(m_PlacingPoint)
			{
				if(	eventType == EventType.MouseDrag )
				{
					float hitDistance = Mathf.Infinity;
					m_Plane.SetNormalAndPosition(polygon.transform.up, polygon.transform.position);

					if( m_Plane.Raycast(ray, out hitDistance) )
					{
						evt.Use();
						polygon.points[m_SelectedIndex] = ProGridsInterface.ProGridsSnap(polygon.transform.InverseTransformPoint(ray.GetPoint(hitDistance)), SNAP_MASK);
						RebuildPolyShapeMesh(false);
						SceneView.RepaintAll();
					}
				}

				if( eventType == EventType.MouseUp ||
					eventType == EventType.Ignore ||
					eventType == EventType.KeyDown ||
					eventType == EventType.KeyUp )
				{
					m_PlacingPoint = false;
					m_SelectedIndex = -1;
					SceneView.RepaintAll();
				}
			}
			else if(polygon.polyEditMode == PolyShape.PolyEditMode.Path)
			{
				if( eventType == EventType.MouseDown )
				{
					if(polygon.points.Count < 1)
						SetPlane(evt.mousePosition);

					float hitDistance = Mathf.Infinity;

					m_Plane.SetNormalAndPosition(polygon.transform.up, polygon.transform.position);

					if( m_Plane.Raycast(ray, out hitDistance) )
					{
						evt.Use();
						UndoUtility.RecordObject(polygon, "Add Polygon Shape Point");

						Vector3 hit = ray.GetPoint(hitDistance);

						if(polygon.points.Count < 1)
							polygon.transform.position = polygon.isOnGrid ? ProGridsInterface.ProGridsSnap(hit) : hit;

						Vector3 point = ProGridsInterface.ProGridsSnap(polygon.transform.InverseTransformPoint(hit), SNAP_MASK);

						if(polygon.points.Count > 2 && ProBuilderMath.Approx3(polygon.points[0], point))
						{
							m_NextMouseUpAdvancesMode = true;
							return;
						}

						polygon.points.Add(point);

						m_PlacingPoint = true;
						m_SelectedIndex = polygon.points.Count - 1;
						RebuildPolyShapeMesh(polygon);
					}
				}
			}
			else if(polygon.polyEditMode == PolyShape.PolyEditMode.Edit)
			{
				if(polygon.points.Count < 3)
				{
					SetPolyEditMode(PolyShape.PolyEditMode.Path);
					return;
				}

				if(m_DistanceFromHeightHandle > PreferenceKeys.k_MaxPointDistanceFromControl)
				{
					// point insertion
					int index;
					float distanceToLine;

					Vector3 p = EditorHandleUtility.ClosestPointToPolyLine(polygon.points, out index, out distanceToLine, true, polygon.transform);
					Vector3 wp = polygon.transform.TransformPoint(p);

					Vector2 ga = HandleUtility.WorldToGUIPoint(polygon.transform.TransformPoint(polygon.points[index % polygon.points.Count]));
					Vector2 gb = HandleUtility.WorldToGUIPoint(polygon.transform.TransformPoint(polygon.points[(index - 1)]));

					Vector2 mouse = evt.mousePosition;

					float distanceToVertex = Mathf.Min(Vector2.Distance(mouse, ga), Vector2.Distance(mouse, gb));

					if(distanceToVertex > PreferenceKeys.k_MaxPointDistanceFromControl && distanceToLine < PreferenceKeys.k_MaxPointDistanceFromControl)
					{
						Handles.color = Color.green;

						Handles.DotHandleCap(-1, wp, Quaternion.identity, HandleUtility.GetHandleSize(wp) * .05f, evt.type);

						if( evt.type == EventType.MouseDown )
						{
							evt.Use();

							UndoUtility.RecordObject(polygon, "Insert Point");
							polygon.points.Insert(index, p);
							m_SelectedIndex = index;
							m_PlacingPoint = true;
							RebuildPolyShapeMesh(true);
							OnBeginVertexMovement();
						}

						Handles.color = Color.white;
					}
				}
			}
		}

		void DoExistingPointsGUI()
		{
			Transform trs = polygon.transform;
			int len = polygon.points.Count;
			Vector3 up = polygon.transform.up;
			Vector3 right = polygon.transform.right;
			Vector3 forward = polygon.transform.forward;
			Vector3 center = Vector3.zero;

			Event evt = Event.current;

			bool used = evt.type == EventType.Used;

			if(!used &&
				(	evt.type == EventType.MouseDown &&
					evt.button == 0 &&
					!IsAppendModifier(evt.modifiers)
				)
			)
			{
				m_SelectedIndex = -1;
				Repaint();
			}

			if(polygon.polyEditMode == PolyShape.PolyEditMode.Height)
			{
				if(!used && evt.type == EventType.MouseUp && evt.button == 0 && !IsAppendModifier(evt.modifiers))
					SetPolyEditMode(PolyShape.PolyEditMode.Edit);

				bool sceneInUse = EditorHandleUtility.SceneViewInUse(evt);
				Ray r = HandleUtility.GUIPointToWorldRay(evt.mousePosition);

				Vector3 origin = polygon.transform.TransformPoint(ProBuilderMath.Average(polygon.points));

				float extrude = polygon.extrude;

				if(!sceneInUse)
				{
					Vector3 p = ProBuilderMath.GetNearestPointRayRay(origin, up, r.origin, r.direction);
					extrude = ProGridsInterface.ProGridsSnap(m_HeightMouseOffset + Vector3.Distance(origin, p) * Mathf.Sign(Vector3.Dot(p-origin, up)));
				}

				Vector3 extrudePoint = origin + (extrude * up);

				Handles.color = HANDLE_COLOR;
				Handles.DotHandleCap(-1, origin, Quaternion.identity, HandleUtility.GetHandleSize(origin) * .05f, evt.type);
				Handles.color = HANDLE_GREEN;
				Handles.DrawLine(origin, extrudePoint);
				Handles.DotHandleCap(-1, extrudePoint, Quaternion.identity, HandleUtility.GetHandleSize(extrudePoint) * .05f, evt.type);
				Handles.color = Color.white;

				if( !sceneInUse && polygon.extrude != extrude)
				{
					OnBeginVertexMovement();
					polygon.extrude = extrude;
					RebuildPolyShapeMesh(false);
				}
			}
			else
			{
				// vertex dots
				for(int ii = 0; ii < len; ii++)
				{
					Vector3 point = trs.TransformPoint(polygon.points[ii]);

					center.x += point.x;
					center.y += point.y;
					center.z += point.z;

					float size = HandleUtility.GetHandleSize(point) * .05f;

					Handles.color = ii == m_SelectedIndex ? SELECTED_COLOR : HANDLE_COLOR;

					EditorGUI.BeginChangeCheck();

					point = Handles.Slider2D(point, up, right, forward, size, Handles.DotHandleCap, Vector2.zero, true);

					if(EditorGUI.EndChangeCheck())
					{
						UndoUtility.RecordObject(polygon, "Move Polygon Shape Point");
						polygon.points[ii] = ProGridsInterface.ProGridsSnap(trs.InverseTransformPoint(point), SNAP_MASK);
						OnBeginVertexMovement();
						RebuildPolyShapeMesh(false);
					}

					// "clicked" a button
					if( !used && evt.type == EventType.Used )
					{
						if(ii == 0 && polygon.points.Count > 2 && polygon.polyEditMode == PolyShape.PolyEditMode.Path)
						{
							m_NextMouseUpAdvancesMode = true;
							return;
						}
						else
						{
							used = true;
							m_SelectedIndex = ii;
						}
					}
				}

				Handles.color = Color.white;

				// height setting
				if(polygon.polyEditMode != PolyShape.PolyEditMode.Path && polygon.points.Count > 2)
				{
					center.x /= (float) len;
					center.y /= (float) len;
					center.z /= (float) len;

					Vector3 extrude = center + (up * polygon.extrude);
					m_DistanceFromHeightHandle = Vector2.Distance(HandleUtility.WorldToGUIPoint(extrude), evt.mousePosition);

					EditorGUI.BeginChangeCheck();

					Handles.color = HANDLE_COLOR;
					Handles.DotHandleCap(-1, center, Quaternion.identity, HandleUtility.GetHandleSize(center) * .05f, evt.type);
					Handles.DrawLine(center, extrude);
					Handles.color = HANDLE_GREEN;
					extrude = Handles.Slider(extrude, up, HandleUtility.GetHandleSize(extrude) * .05f, Handles.DotHandleCap, 0f);
					Handles.color = Color.white;

					if(EditorGUI.EndChangeCheck())
					{
						UndoUtility.RecordObject(polygon, "Set Polygon Shape Height");
						polygon.extrude = ProGridsInterface.ProGridsSnap(Vector3.Distance(extrude, center) * Mathf.Sign(Vector3.Dot(up, extrude - center)));
						OnBeginVertexMovement();
						RebuildPolyShapeMesh(false);
					}
				}
			}
		}

		bool IsAppendModifier(EventModifiers em)
		{
			return 	(em & EventModifiers.Shift) == EventModifiers.Shift ||
					(em & EventModifiers.Control) == EventModifiers.Control ||
					(em & EventModifiers.Alt) == EventModifiers.Alt ||
					(em & EventModifiers.Command) == EventModifiers.Command;
		}

		void HandleKeyEvent(KeyCode key, EventModifiers modifier)
		{
			switch(key)
			{
				case KeyCode.Space:
				case KeyCode.Return:
				{
					if( polygon.polyEditMode == PolyShape.PolyEditMode.Path )
					{
						if( SceneCameraIsAlignedWithPolyUp() )
							SetPolyEditMode(PolyShape.PolyEditMode.Edit);
						else
							SetPolyEditMode(PolyShape.PolyEditMode.Height);
					}
					else if( polygon.polyEditMode == PolyShape.PolyEditMode.Height )
						SetPolyEditMode(PolyShape.PolyEditMode.Edit);
					else if( polygon.polyEditMode == PolyShape.PolyEditMode.Edit )
						SetPolyEditMode(PolyShape.PolyEditMode.None);

					break;
				}

				case KeyCode.Backspace:
				{
					if(m_SelectedIndex > -1)
					{
						UndoUtility.RecordObject(polygon, "Delete Selected Points");
						polygon.points.RemoveAt(m_SelectedIndex);
						m_SelectedIndex = -1;
						RebuildPolyShapeMesh(polygon);
					}
					break;
				}

				case KeyCode.Escape:
				{
					if(polygon.polyEditMode == PolyShape.PolyEditMode.Path || polygon.polyEditMode == PolyShape.PolyEditMode.Height)
					{
						Undo.DestroyObjectImmediate(polygon.gameObject);
					}
					else if(polygon.polyEditMode == PolyShape.PolyEditMode.Edit)
					{
						SetPolyEditMode(PolyShape.PolyEditMode.None);
					}

					break;
				}
			}
		}

		void DrawPolyLine(List<Vector3> points)
		{
			if(points.Count < 2)
				return;

			int vc = polygon.polyEditMode == PolyShape.PolyEditMode.Path ? points.Count : points.Count + 1;

			Vector3[] ver = new Vector3[vc];
			Vector2[] uvs = new Vector2[vc];
			int[] indices = new int[vc];
			int cnt = points.Count;
			float distance = 0f;

			for(int i = 0; i < vc; i++)
			{
				Vector3 a = points[i % cnt];
				Vector3 b = points[i < 1 ? 0 : i - 1];

				float d = Vector3.Distance(a, b);
				distance += d;

				ver[i] = points[i % cnt];
				uvs[i] = new Vector2(distance, 1f);
				indices[i] = i;
			}

			m_LineMesh.Clear();
			m_LineMesh.name = "Poly Shape Guide";
			m_LineMesh.vertices = ver;
			m_LineMesh.uv = uvs;
			m_LineMesh.SetIndices(indices, MeshTopology.LineStrip, 0);
			m_LineMaterial.SetFloat("_LineDistance", distance);
		}

		/**
		 *	Is the scene camera looking directly at the up vector of the current polygon?
		 *	Prevents a situation where the height tool is rendered useless by coplanar
		 *	ray tracking.
		 */
		bool SceneCameraIsAlignedWithPolyUp()
		{
			float dot = Vector3.Dot(SceneView.lastActiveSceneView.camera.transform.forward, polygon.transform.up);
			return Mathf.Abs(Mathf.Abs(dot) - 1f) < .01f;
		}

		void OnEditLevelChange(int editLevel)
		{
			if( polygon != null && polygon.polyEditMode != PolyShape.PolyEditMode.None && ((EditLevel)editLevel) != EditLevel.Plugin)
				polygon.polyEditMode = PolyShape.PolyEditMode.None;
		}

		void OnBeginVertexMovement()
		{
			if(!m_IsModifyingVertices)
				m_IsModifyingVertices = true;
		}

		void OnFinishVertexMovement()
		{
			m_IsModifyingVertices = false;
			RebuildPolyShapeMesh(polygon);
		}

		void UndoRedoPerformed()
		{
			if(m_LineMesh != null)
				DestroyImmediate(m_LineMesh);

			if(m_LineMaterial != null)
				DestroyImmediate(m_LineMaterial);

			m_LineMesh = new Mesh();
			m_LineMaterial = CreateHighlightLineMaterial();

			if(polygon.polyEditMode != PolyShape.PolyEditMode.None)
				RebuildPolyShapeMesh(polygon);
		}
	}
}

