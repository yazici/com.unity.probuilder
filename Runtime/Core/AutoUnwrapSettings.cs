using UnityEngine;

namespace UnityEngine.ProBuilder
{
	/// <summary>
	/// Container for UV mapping parameters per face.
	/// </summary>
	[System.Serializable]
	public class AutoUnwrapSettings
	{
		// Defines the anchor point of UV calculations.
		[System.Obsolete("See pb_UV.Anchor")]
		public enum Justify
		{
			Right,
			Left,
			Top,
			Center,
			Bottom,
			None
		}

		/// <summary>
		/// The point from which UV transform operations will be performed.
		/// </summary>
		public enum Anchor
		{
			UpperLeft,
			UpperCenter,
			UpperRight,
			MiddleLeft,
			MiddleCenter,
			MiddleRight,
			LowerLeft,
			LowerCenter,
			LowerRight,
			None
		}

		/// <summary>
		/// Fill mode.
		/// </summary>
		public enum Fill
		{
			Fit,
			Tile,
			Stretch
		}

		public bool useWorldSpace { get; set; }

		/// <summary>
		/// If true, UV coordinates are calculated using world points instead of local.
		/// </summary>
		public bool flipU { get; set; }

		/// <summary>
		/// If true, the U value will be inverted.
		/// </summary>
		public bool flipV { get; set; }

		/// <summary>
		/// If true, the V value will be inverted.
		/// </summary>
		public bool swapUV { get; set; }

		/// <summary>
		/// If true, U and V values will switched.
		/// </summary>
		public Fill fill { get; set; }

		/// <summary>
		/// Which Fill mode to use.
		/// </summary>
		public Vector2 scale { get; set; }

		/// <summary>
		/// The scale to be applied to U and V coordinates.
		/// </summary>
		public Vector2 offset { get; set; }

		/// <summary>
		/// The offset to be applied to the UV coordinates.
		/// </summary>
		public float rotation { get; set; }

		/// <summary>
		/// Rotates UV coordinates.
		/// </summary>
		#pragma warning disable 0618
		[System.Obsolete("Please use pb_UV.anchor.")]
		#pragma warning disable 0618
		public Justify justify { get; set; }
		#pragma warning restore 0618

		/// <summary>
		/// Aligns UVs to the edges or center.
		/// </summary>
		public Vector2 localPivot { get; set; }

		/// <summary>
		/// The center point of the mapped UVs prior to offset application.
		/// </summary>
		[System.Obsolete("localPivot and localSize are no longer stored.")]
		public Vector2 localSize { get; set; }

		/// <summary>
		/// The size of the mapped UVs prior to modifications.
		/// </summary>
		public Anchor anchor { get; set; }

		public AutoUnwrapSettings()
		{
			this.useWorldSpace = false;
			this.flipU = false;
			this.flipV = false;
			this.swapUV = false;
			this.fill = Fill.Tile;
			this.scale = new Vector2(1f, 1f);
			this.offset = new Vector2(0f, 0f);
			this.rotation = 0f;
			this.anchor = Anchor.None;
		}

		public AutoUnwrapSettings(AutoUnwrapSettings uvs)
		{
            if (uvs == null)
                return;

			this.useWorldSpace = uvs.useWorldSpace;
			this.flipU = uvs.flipU;
			this.flipV = uvs.flipV;
			this.swapUV = uvs.swapUV;
			this.fill = uvs.fill;
			this.scale = uvs.scale;
			this.offset = uvs.offset;
			this.rotation = uvs.rotation;
			this.anchor = uvs.anchor;
		}

		/// <summary>
		/// Reset all UV parameters to default values.
		/// </summary>
		public void Reset()
		{
			this.useWorldSpace = false;
			this.flipU = false;
			this.flipV = false;
			this.swapUV = false;
			this.fill = Fill.Tile;
			this.scale = new Vector2(1f, 1f);
			this.offset = new Vector2(0f, 0f);
			this.rotation = 0f;
			this.anchor = Anchor.LowerLeft;
		}

		public override string ToString()
		{
			string str =
				"Use World Space: " + useWorldSpace + "\n" +
				"Flip U: " + flipU + "\n" +
				"Flip V: " + flipV + "\n" +
				"Swap UV: " + swapUV + "\n" +
				"Fill Mode: " + fill + "\n" +
				"Anchor: " + anchor + "\n" +
				"Scale: " + scale + "\n" +
				"Offset: " + offset + "\n" +
				"Rotation: " + rotation + "\n" +
				"Pivot: " + localPivot + "\n";
			return str;
		}
	}
}
