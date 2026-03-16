using Eventask.App.Models;
using Eventask.App.Services.Generated;
using Refit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Eventask.App.Services
{
	public class AttachmentService : IAttachmentService
	{
		private readonly IEventaskApi _api;
		private readonly ICalendarStateService _calendarStateService;
		private readonly string _downloadFolder;

		public AttachmentService(IEventaskApi api, ICalendarStateService calendarStateService)
		{
			_api = api;
			_calendarStateService = calendarStateService;
			
			// 设置下载文件夹到用户文档目录
			var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
			_downloadFolder = Path.Combine(documentsPath, "Eventask", "Attachments");
			
			// 确保文件夹存在
			Directory.CreateDirectory(_downloadFolder);
		}

		public async Task<AttachmentModel> UploadAttachmentAsync(Guid scheduleItemId, string filePath)
		{
			if (!File.Exists(filePath))
			{
				throw new FileNotFoundException("文件不存在", filePath);
			}

			var fileInfo = new FileInfo(filePath);
			var fileName = fileInfo.Name;

			// 读取文件流
			await using var fileStream = File.OpenRead(filePath);
			
			// 创建 StreamPart 用于上传
			var streamPart = new StreamPart(fileStream, fileName, GetContentType(fileName));

			// 从 CalendarStateService 获取当前日历ID
			// 先尝试确保有日历被选中,如果失败则使用当前值
			var calendarId = await _calendarStateService.EnsureCalendarSelectedAsync();
			
			if (calendarId == Guid.Empty)
			{
				// 如果仍然没有日历,尝试使用当前值(可能在并发场景下已被设置)
				calendarId = _calendarStateService.CurrentCalendarId;
			}

			if (calendarId == Guid.Empty)
			{
				throw new InvalidOperationException("没有选择日历,无法上传附件。请先选择一个日历。");
			}

			// 调用 API 上传
			var dto = await _api.AttachmentsPostAsync(calendarId, scheduleItemId, streamPart);

			// 转换为模型
			var model = AttachmentModel.FromDto(dto);
			model.LocalFilePath = filePath;

			return model;
		}

		public async Task<string> DownloadAttachmentAsync(Guid attachmentId, string fileName)
		{
			// 调用 API 下载
			var response = await _api.DownloadAsync(attachmentId);

			// 检查是否是重定向
			if (response.StatusCode == System.Net.HttpStatusCode.Redirect || 
			    response.StatusCode == System.Net.HttpStatusCode.Found)
			{
				// 获取重定向 URL
				var redirectUrl = response.Headers.Location;
				if (redirectUrl != null)
				{
					using var httpClient = new System.Net.Http.HttpClient();
					var content = await httpClient.GetByteArrayAsync(redirectUrl);
					return await SaveFileAsync(fileName, content);
				}
			}

			// 直接下载文件内容
			var fileContent = await response.Content.ReadAsByteArrayAsync();
			return await SaveFileAsync(fileName, fileContent);
		}

		public async Task<List<AttachmentModel>> GetAttachmentsAsync(Guid scheduleItemId)
		{
			try
			{
				// 从 CalendarStateService 获取当前日历ID
				var calendarId = _calendarStateService.CurrentCalendarId;
				
				if (calendarId == Guid.Empty)
				{
					// 尝试加载日历
					calendarId = await _calendarStateService.EnsureCalendarSelectedAsync();
				}
				
				if (calendarId == Guid.Empty)
				{
					Debug.WriteLine("没有选择日历,无法获取附件列表");
					return new List<AttachmentModel>();
				}

				// 调用 API 获取附件列表
				var attachments = await _api.AttachmentsGetAsync(calendarId, scheduleItemId);
				
				return attachments.Select(dto =>
				{
					var model = AttachmentModel.FromDto(dto);
					return model;
				}).ToList();
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"获取附件列表失败: {ex.Message}");
				return new List<AttachmentModel>();
			}
		}

		public async Task OpenAttachmentAsync(AttachmentModel attachment)
		{
			string filePath;

			// 如果本地有缓存,直接打开
			if (!string.IsNullOrEmpty(attachment.LocalFilePath) && File.Exists(attachment.LocalFilePath))
			{
				filePath = attachment.LocalFilePath;
			}
			else
			{
				// 下载到本地
				filePath = await DownloadAttachmentAsync(attachment.Id, attachment.FileName);
				attachment.LocalFilePath = filePath;
			}

			// 使用系统默认应用打开文件
			try
			{
				var psi = new ProcessStartInfo
				{
					FileName = filePath,
					UseShellExecute = true
				};
				Process.Start(psi);
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException($"无法打开文件: {ex.Message}", ex);
			}
		}

		private async Task<string> SaveFileAsync(string fileName, byte[] content)
		{
			// 生成唯一文件名避免冲突
			var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
			var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
			var extension = Path.GetExtension(fileName);
			var uniqueFileName = $"{fileNameWithoutExt}_{timestamp}{extension}";
			
			var filePath = Path.Combine(_downloadFolder, uniqueFileName);

			await File.WriteAllBytesAsync(filePath, content);

			return filePath;
		}

		private static string GetContentType(string fileName)
		{
			var extension = Path.GetExtension(fileName).ToLowerInvariant();
			
			return extension switch
			{
				".pdf" => "application/pdf",
				".doc" => "application/msword",
				".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
				".xls" => "application/vnd.ms-excel",
				".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
				".ppt" => "application/vnd.ms-powerpoint",
				".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
				".txt" => "text/plain",
				".jpg" or ".jpeg" => "image/jpeg",
				".png" => "image/png",
				".gif" => "image/gif",
				".zip" => "application/zip",
				".rar" => "application/x-rar-compressed",
				_ => "application/octet-stream"
			};
		}
	}
}