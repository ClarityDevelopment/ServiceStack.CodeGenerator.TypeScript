namespace ServiceStack.CodeGenerator.TypeScript.Tests {
    using System;

    using Xunit;

    [Route("/Test/ReturnVoid")]
    public class VoidReturnRoute : IReturnVoid {}

    /// <summary>
    /// This is summary documentation
    /// </summary>
    [Route("/Test/ReturnString")]
    public class ReturnString : IReturn<string> {}

    [Route("/Test/ReturnStringWithParam")]
    public class ReturnStringWithParam : IReturn<string> {
        #region Public Properties

        /// <summary>
        /// Summary documentation regarding "Flag"
        /// </summary>
        [ApiMember]
        public bool Flag { get; set; }

        #endregion
    }

    [Route("/Test/{ID}/ReturnVoid")]
    public class RouteWithParam : IReturnVoid {
        #region Public Properties

        [ApiMember(Description = "API Member Description about Flag")]
        public bool Flag { get; set; }

        [ApiMember]
        public int ID { get; set; }

        #endregion
    }

    /// <summary>
    /// Route Summary
    /// </summary>
    [Route("/Test/{ID}/ReturnDTO")]
    public class RouteWithDTO : IReturn<SomeDto> {
        #region Public Properties

        [ApiMember(Description = "API Member Description about Flag")]
        public bool Flag { get; set; }

        [ApiMember]
        public int ID { get; set; }

        #endregion
    }

    /// <summary>
    /// DTO Summary
    /// </summary>
    public class SomeDto {
        public int ID { get; set; }
        /// <summary>
        /// Property Summary
        /// </summary>
        public string Value { get; set; }

    }
    public class SimpleRouteTest {
        #region Public Methods and Operators

        [Fact]
        public void SimpleRoute() {
            var cg = new TypescriptCodeGenerator(new Type[] { typeof(RouteWithParam) }, "cv.cef.api", new string[] { });
        }

        #endregion
    }
}