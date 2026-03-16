using Eventask.App.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Eventask.App.Services
{
	public interface IAttachmentService
	{
		/// <summary>
		/// 上传附件到服务器
		/// </summary>
		Task<AttachmentModel> UploadAttachmentAsync(Guid scheduleItemId, string filePath);

		/// <summary>
		/// 下载附件到本地
		/// </summary>
		Task<string> DownloadAttachmentAsync(Guid attachmentId, string fileName);

		/// <summary>
		/// 获取日程项的所有附件
		/// </summary>
		Task<List<AttachmentModel>> GetAttachmentsAsync(Guid scheduleItemId);

		/// <summary>
		/// 打开附件文件
		/// </summary>
		Task OpenAttachmentAsync(AttachmentModel attachment);
	}
}