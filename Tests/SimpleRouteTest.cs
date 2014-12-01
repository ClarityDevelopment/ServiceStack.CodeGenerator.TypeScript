﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clarity.TypeScript.CodeGenerator.Tests
{
    using ServiceStack;

    [Route("/Test/ReturnVoid")]
    public class VoidReturnRoute : IReturnVoid
    {
        
    }

    [Route("/Test/ReturnString")]
    public class ReturnString : IReturn<string>
    {

    }

    [Route("/Test/ReturnStringWithParam")]
    public class ReturnStringWithParam : IReturn<string>
    {
        [ApiMember]
        public bool Flag { get; set; }
    }

    [Route("/Test/{ID}/ReturnVoid")]
    public class RouteWithParam : IReturnVoid
    {
        [ApiMember]
        public int ID { get; set; }
        [ApiMember]
        public bool Flag { get; set; }
    }

    public class SimpleRouteTest
    {
        [Xunit.Fact]
        public void SimpleRoute()
        {
            var cg = new TypescriptCodeGenerator(typeof(RouteWithParam), "cv.cef.api", new string[]{});
        }
    }
}
