﻿using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation.Runspaces;
using System.Management.Automation;
using System.Management.Automation.Language;
using Microsoft.Windows.Powershell.ScriptAnalyzer.Generic;
using System.ComponentModel.Composition;
using System.Resources;
using System.Globalization;
using System.Threading;
using Microsoft.Windows.Powershell.ScriptAnalyzer;
using System.Reflection;

namespace Microsoft.Windows.Powershell.ScriptAnalyzer.BuiltinRules
{
    /// <summary>
    /// Use CmdletCorrectly: Check that cmdlets are invoked with the correct mandatory parameter
    /// </summary>
    [Export(typeof(IScriptRule))]
    public class UseCmdletCorrectly : IScriptRule
    {
        /// <summary>
        /// AnalyzeScript: Check that cmdlets are invoked with the correct mandatory parameter
        /// </summary>
        /// <param name="ast"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public IEnumerable<DiagnosticRecord> AnalyzeScript(Ast ast, string fileName)
        {
            if (ast == null) throw new ArgumentNullException(Strings.NullAstErrorMessage);

            // Finds all CommandAsts.
            IEnumerable<Ast> foundAsts = ast.FindAll(testAst => testAst is CommandAst, true);

            // Iterates all CommandAsts and check the command name.
            foreach (Ast foundAst in foundAsts)
            {
                CommandAst cmdAst = (CommandAst)foundAst;

                // Handles the exception caused by commands like, {& $PLINK $args 2> $TempErrorFile}.
                // You can also review the remark section in following document,
                // MSDN: CommandAst.GetCommandName Method
                if (cmdAst.GetCommandName() == null) continue;

                // Checks mandatory parameters.
                if (!IsMandatoryParameterExisted(cmdAst))
                {
                    yield return new DiagnosticRecord(String.Format(CultureInfo.CurrentCulture, Strings.UseCmdletCorrectlyError, cmdAst.GetCommandName()),
                        cmdAst.Extent, GetName(), DiagnosticSeverity.Warning, fileName);
                }
            }
        }

        /// <summary>
        /// Return true if mandatory parameters are used OR the cmdlet does not exist
        /// </summary>
        /// <param name="cmdAst"></param>
        /// <returns></returns>
        private bool IsMandatoryParameterExisted(CommandAst cmdAst)
        {
            CommandInfo cmdInfo = null;
            List<ParameterMetadata> mandParams = new List<ParameterMetadata>();
            IEnumerable<CommandElementAst> ceAsts = null;
            bool returnValue = false;

            #region Predicates

            // Predicate to find ParameterAsts.
            Func<CommandElementAst, bool> foundParamASTs = delegate(CommandElementAst ceAst)
            {
                if (ceAst is CommandParameterAst) return true;
                return false;
            };

            #endregion

            #region Compares parameter list and mandatory parameter list.

            cmdInfo = Helper.Instance.GetCommandInfo(Helper.Instance.GetCmdletNameFromAlias(cmdAst.GetCommandName()))
                ?? Helper.Instance.GetCommandInfo(cmdAst.GetCommandName());

            if (cmdInfo == null || (cmdInfo.CommandType != System.Management.Automation.CommandTypes.Cmdlet))
            {
                return true;
            }

            // Gets parameters from command elements.
            ceAsts = cmdAst.CommandElements.Where<CommandElementAst>(foundParamASTs);

            // Gets mandatory parameters from cmdlet.
            // If cannot find any mandatory parameter, it's not necessary to do a further check for current cmdlet.
            try
            {
                int noOfParamSets = cmdInfo.ParameterSets.Count; 
                foreach (ParameterMetadata pm in cmdInfo.Parameters.Values)
                {
                    int count = 0;

                    if (pm.Attributes.Count < noOfParamSets)
                    {
                        continue;
                    }

                    foreach (Attribute attr in pm.Attributes)
                    {
                        if (!(attr is ParameterAttribute)) continue;
                        if (((ParameterAttribute)attr).Mandatory)
                        {
                            count += 1;
                        }
                    }

                    if (count >= noOfParamSets)
                    {
                        mandParams.Add(pm);
                    }
                }
            }
            catch (Exception)
            {
                // For cases like cmd.exe. Also for runtime exception
                return true;
            }

            if (mandParams.Count() == 0 || Helper.Instance.PositionalParameterUsed(cmdAst))
            {
                returnValue = true;
            }
            else
            {
                // Compares parameter list and mandatory parameter list.
                foreach (CommandElementAst ceAst in ceAsts)
                {
                    CommandParameterAst cpAst = (CommandParameterAst)ceAst;
                    if (mandParams.Count<ParameterMetadata>(item =>
                        item.Name.Equals(cpAst.ParameterName, StringComparison.OrdinalIgnoreCase)) > 0)
                    {
                        returnValue = true;
                        break;
                    }
                }
            }

            #endregion

            return returnValue;
        }
        
        /// <summary>
        /// GetName: Retrieves the name of this rule.
        /// </summary>
        /// <returns>The name of this rule</returns>
        public string GetName()
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.NameSpaceFormat, GetSourceName(), Strings.UseCmdletCorrectlyName);
        }

        /// <summary>
        /// GetCommonName: Retrieves the common name of this rule.
        /// </summary>
        /// <returns>The common name of this rule</returns>
        public string GetCommonName()
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.UseCmdletCorrectlyCommonName);
        }

        /// <summary>
        /// GetDescription: Retrieves the description of this rule.
        /// </summary>
        /// <returns>The description of this rule</returns>
        public string GetDescription()
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.UseCmdletCorrectlyDescription);
        }

        /// <summary>
        /// GetSourceType: Retrieves the type of the rule: builtin, managed or module.
        /// </summary>
        public SourceType GetSourceType()
        {
            return SourceType.Builtin;
        }

        /// <summary>
        /// GetSourceName: Retrieves the module/assembly name the rule is from.
        /// </summary>
        public string GetSourceName()
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.SourceName);
        }
    }
}