using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Vucem.Microservice.Api.Dto
{
    public class PeticionConsultaCoveDto
    {
        public string EDocumentCove { get; set; }

        public long? NumeroOperacion { get; set; }

        public string UsuarioWcf { get; set; }
        public string PasswordWcf { get; set; }
    }
}