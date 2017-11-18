// dotnet-symbol-service
//
// Copyright (C) 2017 Applied Visions - http://securedecisions.avi.com
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using Mono.Cecil;
using SymbolService.Model;

namespace SymbolService.Controllers
{
    [Produces("application/json")]
    [Route("api/Methods")]
    public class MethodsController : Controller
	{
		[HttpPost]
		public IEnumerable<MethodInfo> Methods(IEnumerable<IFormFile> files)
		{
			try
			{
				// TODO: This POST should be binding the file from a multipart/form-data request. But it's not, even though the files do exist in the HttpContext.
				// TODO: Need to find the issue and fix. It could be an improper boundary format in the request.
				var hackFiles = HttpContext.Request.Form.Files;

				var assembly = hackFiles.First(file => file.Name == "Assembly");
				var symbols = hackFiles.First(file => file.Name == "Symbols");

				var (assemblyPath, symbolsPath) = GenerateFilePaths();
				WriteFile(assemblyPath, assembly);
				WriteFile(symbolsPath, symbols);

				var module = LoadModule(assemblyPath, symbolsPath);
				var methodInfos = module.Types.SelectMany(type => type.Methods).Select(method => GetMethodInfo(method));

				return methodInfos;
			}
			catch
			{
				return Enumerable.Empty<MethodInfo>();
			}
		}

		private (string assemblyPath, string symbolsPath) GenerateFilePaths()
		{
			var tempFolder = Path.GetTempPath();
			var randomFile = Path.GetRandomFileName();
			var assemblyPath = $"assembly{randomFile}";
			var symbolsPath = $"symbols{randomFile}";
			
			return (Path.Combine(tempFolder, assemblyPath), Path.Combine(tempFolder, symbolsPath));
		}

		private void WriteFile(string filePath, IFormFile file)
		{
			using (var stream = new FileStream(filePath, FileMode.Create))
			{
				file.CopyTo(stream);
			}
		}

		private ModuleDefinition LoadModule(string assemblyPath, string symbolsPath)
		{
			using (var symbolsStream = new FileStream(symbolsPath, FileMode.Open))
			{
				var parameters = new ReaderParameters
				{
					ReadSymbols = true,
					SymbolStream = symbolsStream
				};
				var assembly = AssemblyDefinition.ReadAssembly(assemblyPath, parameters);

				return assembly.MainModule;
			}
		}

		private MethodInfo GetMethodInfo(MethodDefinition method)
		{
			return new MethodInfo
			{
				FullyQualifiedName = method.Name,
				ContainingClass = method.DeclaringType?.FullName,
				AccessModifiers = GetAccessModifiers(method),
				Parameters = GetParameters(method),
				ReturnType = method.ReturnType.FullName,
				Instructions = method.Body?.Instructions?.Count ?? 0
			};
		}

		static readonly Dictionary<Modifier, Func<MethodDefinition, Modifier>> AccessModiferQueries = new Dictionary<Modifier, Func<MethodDefinition, Modifier>>()
		{
			{ Modifier.PUBLIC, (method) => method.IsPublic ? Modifier.PUBLIC : 0 },
			{ Modifier.PRIVATE, (method) => method.IsPrivate ? Modifier.PRIVATE : 0 },
			{ Modifier.ABSTRACT, (method) => method.IsAbstract ? Modifier.ABSTRACT : 0 },
			{ Modifier.STATIC, (method) => method.IsStatic ? Modifier.STATIC : 0 },
			{ Modifier.SYNCHRONIZED, (method) => method.IsSynchronized ? Modifier.SYNCHRONIZED : 0 },
			{ Modifier.FINAL, (method) => method.IsFinal ? Modifier.FINAL : 0 },
			{ Modifier.PROTECTED, (method) => method.IsFamily ? Modifier.PROTECTED : 0 }
		};

		private int GetAccessModifiers(MethodDefinition method)
		{
			return AccessModiferQueries.Values.Select(getModifier => getModifier(method))
				.Aggregate(0, (previous, next) => previous | (int)next);
		}

		private List<String> GetParameters(MethodDefinition method)
		{
			return method.Parameters.Select(parameter => parameter.ParameterType.FullName).ToList();
		}
	}
}