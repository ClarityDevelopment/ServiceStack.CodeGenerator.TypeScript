using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceStack.CodeGenerator.TypeScript {
    using System.CodeDom.Compiler;
    using System.Reflection;
    using System.Runtime.Serialization;

    public partial class TypescriptCodeGenerator {
        private void WriteDtoClasses(IndentedTextWriter writer) {
            writer.WriteLine("// ----- DTOS -----");

            // Write out the DTOs
            foreach (Type dto in _DTOs.OrderBy(t => t.Name)) {
                PropertyInfo[] dtoProperties = dto.GetProperties().Where(prop => prop.CanRead && !prop.HasAttribute<IgnoreDataMemberAttribute>()).ToArray();

                GenerateJsDoc(writer, dto, dtoProperties, true);

                bool isInheritedClass = dto.BaseType != null && dto.BaseType != typeof(object) && _DTOs.Contains(dto.BaseType);

                if (isInheritedClass) {
                    writer.WriteLine("export interface " + dto.Name + " extends " + dto.BaseType.Name + " {");
                }
                else if (dto.IsEnum) {
                    writer.WriteLine("export enum " + dto.Name + " {");
                }
                else {
                    writer.WriteLine("export interface " + dto.Name + " {");
                }
                writer.Indent++;

                if (dto.IsEnum) {
                    foreach (var value in dto.GetEnumValues()) {
                        if (value is Int32) {
                            writer.WriteLine(dto.GetEnumName(value) + " = " + value + ","); 
                        }
                        else {
                            writer.WriteLine(value + ",");
                        }
                    }
                }
                else {
                    foreach (PropertyInfo property in dtoProperties) {
                        try {
                            // Don't redeclare inherited properties
                            if (isInheritedClass && dto.BaseType.GetProperty(property.Name) != null) {
                                continue;
                            }

                            // Property on this class
                            Type returnType = property.GetMethod.ReturnType;
                            // Optional?
                            if (returnType.IsNullableType() || returnType.IsClass()) writer.WriteLine(property.Name + "?: " + DetermineTsType(returnType) + ";");
                            else // Required 
                                writer.WriteLine(property.Name + ": " + DetermineTsType(returnType) + ";");
                        }
                        catch (Exception e) {
                            writer.WriteLine("// ERROR - Unable to emit property " + property.Name);
                            writer.WriteLine("//     " + e.Message);
                        }
                    }
                }

                writer.Indent--;
                writer.WriteLine("}\n");
            }
        }

    }
}
