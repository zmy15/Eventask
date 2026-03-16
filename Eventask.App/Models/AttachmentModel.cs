using Eventask.Domain.Dtos;
using System;

namespace Eventask.App.Models
{
	public class AttachmentModel
	{
		public Guid Id { get; set; }
		public Guid ScheduleItemId { get; set; }
		public string FileName { get; set; } = string.Empty;
		public string ContentType { get; set; } = string.Empty;
		public long Size { get; set; }
		public string? LocalFilePath { get; set; }

		public string DisplaySize
		{
			get
			{
				if (Size < 1024)
					return $"{Size} B";
				if (Size < 1024 * 1024)
					return $"{Size / 1024.0:F1} KB";
				if (Size < 1024 * 1024 * 1024)
					return $"{Size / (1024.0 * 1024.0):F1} MB";
				return $"{Size / (1024.0 * 1024.0 * 1024.0):F1} GB";
			}
		}

		public static AttachmentModel FromDto(AttachmentDto dto)
		{
			return new AttachmentModel
			{
				Id = dto.Id,
				ScheduleItemId = dto.ScheduleItemId,
				FileName = dto.FileName,
				ContentType = dto.ContentType,
				Size = dto.Size
			};
		}
	}
}