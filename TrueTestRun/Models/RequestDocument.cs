using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TrueTestRun.Models
{
    public class RequestDocument
    {
        [Key]
        public int DocumentID { get; set; }

        [Required]
        [MaxLength(50)]
        [ForeignKey("Request")]
        public string RequestID { get; set; }

        [Required]
        [MaxLength(255)]
        public string FileName { get; set; }

        [Required]
        [MaxLength(255)]
        public string OriginalFileName { get; set; }

        [Required]
        [MaxLength(100)]
        public string ContentType { get; set; }

        public long FileSize { get; set; }

        [Required]
        [MaxLength(500)]
        public string FilePath { get; set; }

        [Required]
        [MaxLength(50)]
        public string UploadedByADID { get; set; }

        public DateTime UploadedAt { get; set; }

        [MaxLength(1000)]
        public string Description { get; set; }

        // Navigation properties
        public virtual Request Request { get; set; }
    }
}