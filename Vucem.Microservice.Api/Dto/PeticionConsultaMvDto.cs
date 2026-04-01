using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Vucem.Microservice.Api.Dto
{
    public class PeticionConsultaMvDto
    {
        public string UsuarioWcf { get; set; }
        public string PasswordWcf { get; set; }
        public long NumeroOperacion { get; set; } // El ticket que nos dio VUCEM en la Fase 1
    }
}