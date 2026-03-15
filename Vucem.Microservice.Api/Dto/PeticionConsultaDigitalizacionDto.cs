using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Vucem.Microservice.Api.Dto
{
    public class PeticionConsultaDigitalizacionDto
    {
        public long NumeroOperacion { get; set; } // El folio que te da el SAT cuando se tarda
        public string CadenaOriginal { get; set; }
        public string FirmaBase64 { get; set; }
        public string CertificadoBase64 { get; set; }
        public string UsuarioWcf { get; set; }
        public string PasswordWcf { get; set; }
    }
}