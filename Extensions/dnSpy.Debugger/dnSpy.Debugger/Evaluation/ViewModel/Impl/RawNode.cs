﻿/*
    Copyright (C) 2014-2017 de4dot@gmail.com

    This file is part of dnSpy

    dnSpy is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    dnSpy is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with dnSpy.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using dnSpy.Contracts.Debugger.Evaluation;
using dnSpy.Contracts.Text;
using dnSpy.Debugger.Text;

namespace dnSpy.Debugger.Evaluation.ViewModel.Impl {
	/// <summary>
	/// Base class of nodes containing the data shown in the UI. Some of them contain real debugger data,
	/// others contain cached data or error messages.
	/// </summary>
	abstract class RawNode {
		/// <summary>
		/// Some nodes can delay reading the underlying debugger <see cref="DbgValueNode"/>. If the real
		/// data hasn't been read yet, this property returns false. This is used to prevent reading the
		/// underlying data when re-using nodes.
		/// </summary>
		public virtual bool HasInitializedUnderlyingData => true;
		public abstract bool CanEvaluateExpression { get; }
		public abstract string Expression { get; }
		public abstract string ImageName { get; }
		public abstract bool IsReadOnly { get; }
		public abstract bool? HasChildren { get; }
		public abstract ulong? ChildCount { get; }
		public virtual RawNode CreateChild(Action<ChildDbgValueRawNode, object> debuggerValueNodeChanged, object debuggerValueNodeChangedData, uint index) => throw new NotSupportedException();
		public abstract void Format(DbgEvaluationContext context, IDbgValueNodeFormatParameters options);
		public abstract void FormatName(DbgEvaluationContext context, ITextColorWriter output);
		public abstract void FormatValue(DbgEvaluationContext context, ITextColorWriter output, DbgValueFormatterOptions options);
		public abstract DbgValueNodeAssignmentResult Assign(DbgEvaluationContext context, string expression, DbgEvaluationOptions options);
	}

	sealed class EditRawNode : RawNode {
		public override bool CanEvaluateExpression => false;
		public override string Expression => string.Empty;
		public override string ImageName => PredefinedDbgValueNodeImageNames.Edit;
		public override bool IsReadOnly => true;
		public override bool? HasChildren => false;
		public override ulong? ChildCount => 0;
		public override void Format(DbgEvaluationContext context, IDbgValueNodeFormatParameters options) { }
		public override void FormatName(DbgEvaluationContext context, ITextColorWriter output) { }
		public override void FormatValue(DbgEvaluationContext context, ITextColorWriter output, DbgValueFormatterOptions options) { }
		public override DbgValueNodeAssignmentResult Assign(DbgEvaluationContext context, string expression, DbgEvaluationOptions options) => throw new NotSupportedException();
	}

	sealed class ErrorRawNode : RawNode {
		public override bool CanEvaluateExpression => true;
		public string ErrorMessage => errorMessage;
		public override string Expression => expression;
		public override string ImageName => PredefinedDbgValueNodeImageNames.Error;
		public override bool IsReadOnly => true;
		public override bool? HasChildren => false;
		public override ulong? ChildCount => 0;

		readonly string expression;
		string errorMessage;

		public ErrorRawNode(string expression, string errorMessage) {
			this.expression = expression ?? throw new ArgumentNullException(nameof(expression));
			this.errorMessage = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage));
		}

		internal void SetErrorMessage(string errorMessage) => this.errorMessage = errorMessage;

		public override void Format(DbgEvaluationContext context, IDbgValueNodeFormatParameters options) {
			if (options.NameOutput != null)
				FormatName(context, options.NameOutput);
			if (options.ValueOutput != null)
				FormatValue(context, options.ValueOutput, options.ValueFormatterOptions);
		}

		public override void FormatName(DbgEvaluationContext context, ITextColorWriter output) => output.Write(BoxedTextColor.Text, expression);
		public override void FormatValue(DbgEvaluationContext context, ITextColorWriter output, DbgValueFormatterOptions options) => output.Write(BoxedTextColor.Error, errorMessage);
		public override DbgValueNodeAssignmentResult Assign(DbgEvaluationContext context, string expression, DbgEvaluationOptions options) => throw new NotSupportedException();
	}

	abstract class CachedRawNodeBase : RawNode {
		public sealed override bool IsReadOnly => true;
		public sealed override DbgValueNodeAssignmentResult Assign(DbgEvaluationContext context, string expression, DbgEvaluationOptions options) => throw new NotImplementedException();

		protected abstract ClassifiedTextCollection CachedName { get; }
		protected abstract ClassifiedTextCollection CachedValue { get; }
		protected abstract ClassifiedTextCollection CachedExpectedType { get; }
		protected abstract ClassifiedTextCollection CachedActualType { get; }

		public sealed override void Format(DbgEvaluationContext context, IDbgValueNodeFormatParameters options) {
			if (options.NameOutput != null)
				FormatName(context, options.NameOutput);
			if (options.ValueOutput != null)
				FormatValue(context, options.ValueOutput, options.ValueFormatterOptions);
			if (options.ExpectedTypeOutput != null)
				WriteTo(options.ExpectedTypeOutput, CachedExpectedType);
			if (options.ActualTypeOutput != null)
				WriteTo(options.ActualTypeOutput, CachedActualType);
		}

		public sealed override void FormatName(DbgEvaluationContext context, ITextColorWriter output) => WriteTo(output, CachedName, string.IsNullOrEmpty(Expression) ? UNKNOWN : Expression);
		public sealed override void FormatValue(DbgEvaluationContext context, ITextColorWriter output, DbgValueFormatterOptions options) => WriteTo(output, CachedValue);

		const string UNKNOWN = "???";
		static void WriteTo(ITextColorWriter output, ClassifiedTextCollection coll, string unknownText = UNKNOWN) {
			if (coll.IsDefault) {
				output.Write(BoxedTextColor.Error, unknownText);
				return;
			}

			coll.WriteTo(output);
		}
	}

	sealed class EmptyCachedRawNode : CachedRawNodeBase {
		public static readonly EmptyCachedRawNode Instance = new EmptyCachedRawNode();
		EmptyCachedRawNode() { }

		public override bool CanEvaluateExpression => false;
		public override string Expression => string.Empty;
		public override string ImageName => PredefinedDbgValueNodeImageNames.Error;
		public override bool? HasChildren => false;
		public override ulong? ChildCount => 0;

		protected override ClassifiedTextCollection CachedName => default;
		protected override ClassifiedTextCollection CachedValue => default;
		protected override ClassifiedTextCollection CachedExpectedType => default;
		protected override ClassifiedTextCollection CachedActualType => default;
	}

	sealed class CachedRawNode : CachedRawNodeBase {
		public override bool CanEvaluateExpression { get; }
		public override string Expression { get; }
		public override string ImageName { get; }
		public override bool? HasChildren { get; }
		public override ulong? ChildCount { get; }

		protected override ClassifiedTextCollection CachedName { get; }
		protected override ClassifiedTextCollection CachedValue { get; }
		protected override ClassifiedTextCollection CachedExpectedType { get; }
		protected override ClassifiedTextCollection CachedActualType { get; }

		public CachedRawNode(bool canEvaluateExpression, string expression, string imageName, bool? hasChildren, ulong? childCount, ClassifiedTextCollection cachedName, ClassifiedTextCollection cachedValue, ClassifiedTextCollection cachedExpectedType, ClassifiedTextCollection cachedActualType) {
			CanEvaluateExpression = canEvaluateExpression;
			Expression = expression ?? throw new ArgumentNullException(nameof(expression));
			ImageName = imageName ?? throw new ArgumentNullException(nameof(imageName));
			HasChildren = hasChildren;
			ChildCount = childCount;
			CachedName = cachedName;
			CachedValue = cachedValue;
			CachedExpectedType = cachedExpectedType;
			CachedActualType = cachedActualType.IsDefault ? cachedExpectedType : cachedActualType;
		}
	}

	/// <summary>
	/// Base class of nodes containing actual debugger data (a <see cref="DbgValueNode"/>)
	/// </summary>
	abstract class DebuggerValueRawNode : RawNode {
		public override bool CanEvaluateExpression => DebuggerValueNode.CanEvaluateExpression;
		public override string Expression => DebuggerValueNode.Expression;
		public override string ImageName => DebuggerValueNode.ImageName;
		public override bool IsReadOnly => DebuggerValueNode.IsReadOnly;
		public override bool? HasChildren => DebuggerValueNode.HasChildren;
		public override ulong? ChildCount => DebuggerValueNode.ChildCount;

		internal abstract DbgValueNode DebuggerValueNode { get; }

		protected DbgValueNodeReader reader;

		protected DebuggerValueRawNode(DbgValueNodeReader reader) => this.reader = reader ?? throw new ArgumentNullException(nameof(reader));

		public override RawNode CreateChild(Action<ChildDbgValueRawNode, object> debuggerValueNodeChanged, object debuggerValueNodeChangedData, uint index) =>
			new ChildDbgValueRawNode(debuggerValueNodeChanged, debuggerValueNodeChangedData, this, index, reader);
		public override void Format(DbgEvaluationContext context, IDbgValueNodeFormatParameters options) => DebuggerValueNode.Format(context, options);
		public override void FormatName(DbgEvaluationContext context, ITextColorWriter output) => DebuggerValueNode.FormatName(context, output);
		public override void FormatValue(DbgEvaluationContext context, ITextColorWriter output, DbgValueFormatterOptions options) => DebuggerValueNode.FormatValue(context, output, options);
		public override DbgValueNodeAssignmentResult Assign(DbgEvaluationContext context, string expression, DbgEvaluationOptions options) => DebuggerValueNode.Assign(context, expression, options);
	}

	/// <summary>
	/// This is usually the root node shown in the variables windows, but it could also be a child node if
	/// it got refreshed (user pressed the Refresh icon).
	/// </summary>
	sealed class DbgValueRawNode : DebuggerValueRawNode {
		internal override DbgValueNode DebuggerValueNode { get; }
		public DbgValueRawNode(DbgValueNodeReader reader, DbgValueNode valueNode)
			: base(reader) => DebuggerValueNode = valueNode ?? throw new ArgumentNullException(nameof(valueNode));
	}

	sealed class ChildDbgValueRawNode : DebuggerValueRawNode {
		public override bool HasInitializedUnderlyingData => __dbgValueNode_DONT_USE != null;
		internal DebuggerValueRawNode Parent => parent;
		internal uint DbgValueNodeChildIndex { get; }

		internal override DbgValueNode DebuggerValueNode {
			get {
				var dbgNode = __dbgValueNode_DONT_USE;
				if (dbgNode == null) {
					__dbgValueNode_DONT_USE = dbgNode = reader.GetDebuggerNode(this);
					debuggerValueNodeChanged(this, debuggerValueNodeChangedData);
				}
				return dbgNode;
			}
		}
		DbgValueNode __dbgValueNode_DONT_USE;

		DebuggerValueRawNode parent;
		readonly Action<ChildDbgValueRawNode, object> debuggerValueNodeChanged;
		readonly object debuggerValueNodeChangedData;

		public ChildDbgValueRawNode(Action<ChildDbgValueRawNode, object> debuggerValueNodeChanged, object debuggerValueNodeChangedData, DebuggerValueRawNode parent, uint dbgValueNodeChildIndex, DbgValueNodeReader reader)
			: base(reader) {
			this.debuggerValueNodeChanged = debuggerValueNodeChanged ?? throw new ArgumentNullException(nameof(debuggerValueNodeChanged));
			this.debuggerValueNodeChangedData = debuggerValueNodeChangedData;
			this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
			DbgValueNodeChildIndex = dbgValueNodeChildIndex;
		}

		public ChildDbgValueRawNode(Action<ChildDbgValueRawNode, object> debuggerValueNodeChanged, object debuggerValueNodeChangedData, DebuggerValueRawNode parent, uint dbgValueNodeChildIndex, DbgValueNodeReader reader, DbgValueNode value)
			: base(reader) {
			this.debuggerValueNodeChanged = debuggerValueNodeChanged ?? throw new ArgumentNullException(nameof(debuggerValueNodeChanged));
			this.debuggerValueNodeChangedData = debuggerValueNodeChangedData;
			this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
			DbgValueNodeChildIndex = dbgValueNodeChildIndex;
			__dbgValueNode_DONT_USE = value ?? throw new ArgumentNullException(nameof(value));
		}

		internal void SetParent(DebuggerValueRawNode parent, DbgValueNode newValue) {
			this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
			__dbgValueNode_DONT_USE = newValue;
		}
	}
}