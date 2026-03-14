using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Vucem.Microservice.Api.Dto
{
    public class PeticionDigitalizacionDto
    {
        public string CorreoEmail { get; set; }
        public string RfcConsulta { get; set; }
        public int IdTipoDocumento { get; set; } // ¡Corregido a INT! (Ej. 1 para Facturas)
        public string NombreDocumento { get; set; }

        public string ArchivoBase64 { get; set; } // El PDF convertido a Base64

        public string CadenaOriginal { get; set; }
        public string FirmaBase64 { get; set; }
        public string CertificadoBase64 { get; set; }

        public string UsuarioWcf { get; set; }
        public string PasswordWcf { get; set; }
    }
}