using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Eventask.Domain.Dtos;

namespace Eventask.App.Models
{
	public partial class CalendarItemModel : ObservableObject
	{
		public Guid Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public string? Color { get; set; }
		
		[ObservableProperty]
		private bool _isSelected;

		[ObservableProperty]
		private bool _canDelete = true;
		
		public int Version { get; set; }
		public DateTimeOffset UpdatedAt { get; set; }

		public static CalendarItemModel FromDto(CalendarDto dto)
		{
			return new CalendarItemModel
			{
				Id = dto.Id,
				Name = dto.Name,
				Version = dto.Version,
				UpdatedAt = dto.UpdatedAt
			};
		}
	}
}