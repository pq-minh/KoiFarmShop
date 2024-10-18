﻿using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KoiShop.Application.Dtos.KoiDtos
{
    public class UpdateBatchKoiDto
    {
        public string? BatchKoiName { get; set; }

        public string? Description { get; set; }
        public string? Age { get; set; }

        public string? Quantity { get; set; }

        public string? Weight { get; set; }

        public string? Size { get; set; }

        public string? Origin { get; set; }

        public string? Gender { get; set; }

        public IFormFile? Certificate { get; set; }

        public IFormFile? ImageFile { get; set; }

        public int? BatchTypeId { get; set; }

        public double? Price { get; set; }

        public string? Status { get; set; }
    }
}