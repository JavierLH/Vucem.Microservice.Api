using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Vucem.Microservice.Api.Dto
{
    public class PeticionVucemDto
    {
        public string CadenaOriginal { get; set; }
        public string RfcImportador { get; set; }
        public string FirmaBase64 { get; set; }
        public string CertificadoBase64 { get; set; }
        public string UsuarioWcf { get; set; }
        public string PasswordWcf { get; set; }
    }
}