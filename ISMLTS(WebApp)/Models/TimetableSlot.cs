using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ISMLTS_WebApp_.Models
{
    // A weekly class: the module meets on this day, at these times, in this venue
    public class TimetableSlot
    {
        [Key]
        public int SlotId { get; set; }

        [ForeignKey(nameof(Module))]
        public int ModuleId { get; set; }
        public Module? Module { get; set; }

        public DayOfWeek Day { get; set; }

        [Display(Name = "Starts")]
        public TimeOnly StartTime { get; set; }

        [Display(Name = "Ends")]
        public TimeOnly EndTime { get; set; }

        [Required, MaxLength(60)]
        public string Venue { get; set; } = string.Empty;
    }
}
