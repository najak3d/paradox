// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.
using System.Collections.Generic;

using SiliconStudio.Shaders.Ast;

namespace SiliconStudio.Paradox.Shaders.Parser.Ast
{
    /// <summary>
    /// A Shader block.
    /// </summary>
    public class ShaderBlock : TypeBase, IScopeContainer
    {
        #region Public Properties

        /// <summary>
        /// Gets or sets a value indicating whether this instance is partial.
        /// </summary>
        /// <value><c>true</c> if this instance is partial; otherwise, <c>false</c>.</value>
        public bool IsPartial { get; set; }

        /// <summary>
        /// Gets or sets the body.
        /// </summary>
        /// <value>The body.</value>
        public BlockStatement Body { get; set; }

        #endregion

        #region Public Methods

        /// <inheritdoc />
        public override IEnumerable<Node> Childrens()
        {
            ChildrenList.Clear();
            ChildrenList.Add(Name);
            ChildrenList.Add(Body);
            return ChildrenList;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return string.Format("{0}shader {1} {{...}}", IsPartial ? "partial " : string.Empty, Name);
        }

        #endregion
    }
}