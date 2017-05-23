using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.IO;

namespace ZTranslation.Models
{
	internal class BinaryPatcher
	{
		public virtual byte[] original_hash { get; protected set; }
		public virtual int original_length { get; protected set; }

		protected virtual string match_query => "";
		protected virtual string patched_file => "";

		public BinaryPatcher()
		{
			this.original_hash = new byte[16];
			this.original_length = 0;
		}

		public virtual bool Patch(Session session, ref byte[] data)
		{
			try
			{
				if (!session.Request.PathAndQuery.StartsWith(this.match_query)) return false;
				if (data.Length != original_length) return false;

				byte[] hash;
				using (MD5 md5 = MD5.Create()) hash = md5.ComputeHash(data);
				if (!hash.SequenceEqual(original_hash)) return false;

				data = File.ReadAllBytes(this.patched_file);
				return true;
			}
			catch
			{
				return false;
			}
		}
	}

	internal class FontPatcher : BinaryPatcher
	{
		public static FontPatcher Instance { get; } = new FontPatcher();

		protected override string match_query => "/kcs/resources/swf/font.swf";
		protected override string patched_file => Path.Combine(Common.PatchedFilesDirectory, "font.swf");

		public FontPatcher() : base()
		{
			base.original_hash = new byte[] { 0xca, 0xf9, 0xa2, 0x30, 0xfe, 0x46, 0x8a, 0xfc, 0x3d, 0x23, 0x5b, 0xb8, 0xd2, 0xd8, 0x7e, 0x89 };
			base.original_length = 2232291;
		}
	}
	internal class TitleMainPatcher : BinaryPatcher
	{
		public static TitleMainPatcher Instance { get; } = new TitleMainPatcher();

		protected override string match_query => "/kcs/scenes/TitleMain.swf";
		protected override string patched_file => Path.Combine(Common.PatchedFilesDirectory, "TitleMain.swf");

		public TitleMainPatcher() : base()
		{
			base.original_hash = new byte[] { 0x6d, 0x11, 0xf3, 0xaa, 0x9c, 0xcd, 0x4b, 0x84, 0x08, 0x3a, 0xcd, 0x5f, 0x30, 0x37, 0x10, 0x03 };
			base.original_length = 734438;
		}
	}
	internal class PortMainPatcher : BinaryPatcher
	{
		public static PortMainPatcher Instance { get; } = new PortMainPatcher();

		protected override string match_query => "/kcs/PortMain.swf";
		protected override string patched_file => Path.Combine(Common.PatchedFilesDirectory, "PortMain.swf");

		public PortMainPatcher() : base()
		{
			base.original_hash = new byte[] { 0xe2, 0x6c, 0xe4, 0x4c, 0x3e, 0xce, 0xfa, 0x32, 0x76, 0xde, 0x5e, 0xe0, 0xde, 0x0c, 0x58, 0x71 };
			base.original_length = 1243247;
		}
	}
	internal class OrganizeMainPatcher : BinaryPatcher
	{
		public static OrganizeMainPatcher Instance { get; } = new OrganizeMainPatcher();

		protected override string match_query => "/kcs/scenes/OrganizeMain.swf";
		protected override string patched_file => Path.Combine(Common.PatchedFilesDirectory, "OrganizeMain.swf");

		public OrganizeMainPatcher() : base()
		{
			base.original_hash = new byte[] { 0xd4, 0x20, 0xca, 0xc4, 0xa8, 0xf1, 0x8f, 0xbe, 0x46, 0xf6, 0x7c, 0xf3, 0x82, 0xb9, 0x3c, 0x84 };
			base.original_length = 1428948;
		}
	}
	internal class SupplyMainPatcher : BinaryPatcher
	{
		public static SupplyMainPatcher Instance { get; } = new SupplyMainPatcher();

		protected override string match_query => "/kcs/scenes/SupplyMain.swf";
		protected override string patched_file => Path.Combine(Common.PatchedFilesDirectory, "SupplyMain.swf");

		public SupplyMainPatcher() : base()
		{
			base.original_hash = new byte[] { 0x84, 0xb9, 0xca, 0x41, 0x76, 0x02, 0x63, 0xbb, 0x47, 0x62, 0xad, 0x2b, 0xc2, 0x63, 0x14, 0xb4 };
			base.original_length = 101430;
		}
	}
}
