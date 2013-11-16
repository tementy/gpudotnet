//
// SyntaxTreeBuilder.cs
//
// Author:
//   Artem Lebedev (tementy@gmail.com)
//
// (C) 2012 Rybinsk State Aviation Technical University (http://www.rsatu.ru)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Linq;
using System.Collections.Generic;
using UniGPU.Core.Utils;

namespace UniGPU.Core.IR
{
	public static class SyntaxTreeBuilder
	{
		public static IList<TreeStatement> BuildAST(this Subprogram src)
		{
			IList<BasicBlock> bblist = src.GetBasicBlocks();
			Dictionary<BasicBlock, IEnumerable<BasicBlock>> pred = bblist.Select(bb =>
				new KeyValuePair<BasicBlock, IEnumerable<BasicBlock>>(bb,
					bblist.Where(bbel => bbel.Successor == bb || bbel.Target == bb))).
				ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
			
			// Determine dominators.
			
			Dictionary<BasicBlock, List<BasicBlock>> dom = bblist.Select(bb =>
				new KeyValuePair<BasicBlock, List<BasicBlock>>(bb,
					bb == src.CFGRoot ? new List<BasicBlock> { src.CFGRoot } : bblist.ToList())).
				ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
			
			bool changes = true;
			while (changes)
			{
				changes = false;
				foreach (BasicBlock n in bblist)
				{
					if (n != src.CFGRoot)
					{
						IEnumerable<BasicBlock> doms = null;
						foreach (BasicBlock p in pred[n])
							doms = (doms == null) ? dom[p] : doms.Intersect(dom[p]);
						doms = doms.Union(new [] { n });
						
						if (!doms.SequenceEqual(dom[n]))
						{
							changes = true;
							dom[n] = new List<BasicBlock>(doms);
						}
					}
				}
			}
			
			// Perform depth-first enumeration.
						
			Dictionary<BasicBlock, int> dfn = bblist.Select(bb =>
				new KeyValuePair<BasicBlock, int>(bb, 0)).
				ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
			
			int c = bblist.Count;
			
			NumDepth(src.CFGRoot, dfn, ref c, new List<BasicBlock>());
			
			// Test CFG reducibility: each retreating edge must be back edge.

			foreach (BasicBlock sbb in bblist)
			{
				foreach (BasicBlock tbb in new [] { sbb.Successor, sbb.Target })
				{
					if (tbb != null && dfn[tbb] < dfn[sbb] && !dom[sbb].Contains(tbb))
						throw new IrreducibleCFGException("There is non-back retreating edge");
				}
			}

			if (bblist.Where(bb => bb.Trailer.OpCode == IROpCodes.RET).Count() != 1)
				throw new IrreducibleCFGException("Control flow graph must have single node with RET ending");

			List<TreeStatement> code = new List<TreeStatement>();
			BasicBlock cur = src.CFGRoot;
			GraphPathFinder path_finder = new GraphPathFinder(bblist);
			while (cur != null)
				cur = HandleStatement(cur, null, bblist, pred, dom, path_finder, code);
			return code.AsReadOnly();
		}

		private static BasicBlock HandleStatement(
			BasicBlock start,
			BasicBlock enclosing_loop_frontier,
			IList<BasicBlock> bblist,
			Dictionary<BasicBlock, IEnumerable<BasicBlock>> pred,
			Dictionary<BasicBlock, List<BasicBlock>> dom,
			GraphPathFinder path_finder,
			List<TreeStatement> code)
		{
			BasicBlock loop_tail;
			try
			{
				loop_tail = pred[start].Where(bb => dom[bb].Contains(start)).SingleOrDefault();
			}
			catch (InvalidOperationException e)
			{
				throw new IrreducibleCFGException("Loop must have only one route to its header from its body");
			}
			if (loop_tail != null)
			{
				IEnumerable<BasicBlock> loop_insiders = bblist.Where(
					bb => path_finder.GetPathLength(bb, loop_tail) < (int)short.MaxValue &&
						!path_finder.GetPath(bb, loop_tail).Contains(start) &&
						dom[bb].Contains(start)).Add(start);

				BasicBlock loop_frontier;

				try
				{
					loop_frontier = (from insider in loop_insiders
						from adj in new [] { insider.Target, insider.Successor }
						where adj != null && !loop_insiders.Contains(adj)
						select adj).Single();
				}
				catch (InvalidOperationException e)
				{
					throw new IrreducibleCFGException("Loop must have only one exit route from its body");
				}

				List<TreeStatement> loop_body = start.Code.Select(bbi => new InstructionStatement(bbi) as TreeStatement).ToList();
				BasicBlock statement_cursor = HandleBranch(start.Trailer, loop_frontier, bblist, pred, dom, path_finder, loop_body);
				while (statement_cursor != start)
					statement_cursor = HandleStatement(statement_cursor, loop_frontier, bblist, pred, dom, path_finder, loop_body);

				code.Add(new InfiniteLoopStatement(loop_body));

				return loop_frontier;
			}

			code.AddRange(start.Code.Select(bbi => new InstructionStatement(bbi)));
			return HandleBranch(start.Trailer, enclosing_loop_frontier, bblist, pred, dom, path_finder, code);
		}

		private static BasicBlock HandleBranch(
			ControlFlowInstruction cfi,
			BasicBlock enclosing_loop_frontier,
			IList<BasicBlock> bblist,
			Dictionary<BasicBlock, IEnumerable<BasicBlock>> pred,
			Dictionary<BasicBlock, List<BasicBlock>> dom,
			GraphPathFinder path_finder,
			List<TreeStatement> code)
		{
			JumpInstruction jump = cfi as JumpInstruction;
			JumpIfInstruction jumpif = cfi as JumpIfInstruction;

			switch (cfi.OpCode)
			{
				case IROpCodes.RET:
					return null;
				case IROpCodes.JMP:
					return jump.Target;
				case IROpCodes.JT:
				case IROpCodes.JF:
				{
					// loop condition check

					if (jumpif.Target == enclosing_loop_frontier)
					{
						// while-do break condition check
						code.Add(new BranchStatement(
							jumpif.Flag,
							cfi.OpCode == IROpCodes.JT ? new TreeStatement[] { new BreakStatement() } : null,
							cfi.OpCode == IROpCodes.JF ? new TreeStatement[] { new BreakStatement() } : null));
						return jumpif.Next;
					}
					if (jumpif.Next == enclosing_loop_frontier)
					{
						// do-while continue condition check
						code.Add(new BranchStatement(
							jumpif.Flag,
							cfi.OpCode == IROpCodes.JF ? new TreeStatement[] { new BreakStatement() } : null,
							cfi.OpCode == IROpCodes.JT ? new TreeStatement[] { new BreakStatement() } : null));
						return jumpif.Target;
					}

					// initialize IF statement frontier with enclosing loop frontier or the last block in CFG
					BasicBlock frontier = (enclosing_loop_frontier != null) ? enclosing_loop_frontier :
						bblist.Single(bb => bb.Trailer.OpCode == IROpCodes.RET);

					// precise IF statement frontier with last common block in reversed paths going 
					// from initial frontier value to both successors of basic block ending with this branch
					IEnumerator<BasicBlock> targetPathRev =
						path_finder.GetPath(jumpif.Target, frontier).Reverse().GetEnumerator();
					IEnumerator<BasicBlock> nextPathRev =
						path_finder.GetPath(jumpif.Next, frontier).Reverse().GetEnumerator();
					while (targetPathRev.MoveNext() && nextPathRev.MoveNext() && targetPathRev.Current == nextPathRev.Current)
						frontier = targetPathRev.Current;

					List<TreeStatement> trueBranchCode = new List<TreeStatement>();
					List<TreeStatement> falseBranchCode = new List<TreeStatement>();

					BasicBlock cursor;

					cursor = cfi.OpCode == IROpCodes.JT ? jumpif.Target : jumpif.Next;
					while (cursor != frontier)
						cursor = HandleStatement(cursor, null, bblist, pred, dom, path_finder, trueBranchCode);

					cursor = cfi.OpCode == IROpCodes.JF ? jumpif.Target : jumpif.Next;
					while (cursor != frontier)
						cursor = HandleStatement(cursor, null, bblist, pred, dom, path_finder, falseBranchCode);

					code.Add(new BranchStatement(jumpif.Flag, trueBranchCode, falseBranchCode));

					return frontier;
				}
				default:
					throw new NotSupportedException(cfi.OpCode.ToString());

			}
		}

		private static void NumDepth(BasicBlock n, Dictionary<BasicBlock, int> dfn, 
			ref int c, IList<BasicBlock> visited)
		{
			visited.Add(n);
			foreach (BasicBlock s in new [] { n.Successor, n.Target })
			{
				if (s != null && !visited.Contains(s))
					NumDepth(s, dfn, ref c, visited);
			}
			dfn[n] = c--;
		}

		private class GraphPathFinder
		{
			private IList<BasicBlock> bblist;

			private int[,] adjacency;
			private int[,] distance;
			private int N;

			private void FloydWarshall()
			{
				distance = new int[N, N];

				for (int i = 0; i < N; i++)
				{
					for (int j = 0; j < N; j++)
					{
						distance[i, j] = (i == j) ? 0 : adjacency[i, j];
					}
				}

				for (int m = 0; m < N; m++)
				{
					for (int i = 0; i < N; i++)
					{
						for (int j = 0; j < N; j++)
						{
							distance[i, j] = Math.Min(distance[i, j], distance[i, m] + distance[m, j]);
						}
					}
				}
			}

			private int GetPathLength(int s, int t)
			{
				return distance[s, t];
			}

			private Stack<int> GetPath(int s, int t)
			{
				Stack<int> stack = new Stack<int>();
				if (GetPathLength(s, t) < (int)short.MaxValue)
				{
					stack.Push(t);
					int v = t;
					while (v != s)
					{
						int u = 0;
						while (u < N && distance[s, v] != distance[s, u] + adjacency[u, v]) u++;
						stack.Push(u);
						v = u;
					}
				}
				return stack;
			}

			public int GetPathLength(BasicBlock s, BasicBlock t)
			{
				return GetPathLength(bblist.IndexOf(s), bblist.IndexOf(t));
			}

			public Stack<BasicBlock> GetPath(BasicBlock s, BasicBlock t)
			{
				return new Stack<BasicBlock>(GetPath(bblist.IndexOf(s), bblist.IndexOf(t)).Select(
					i => bblist[i]).Reverse());
			}

			public GraphPathFinder(IList<BasicBlock> bblist)
			{
				this.bblist = bblist;
				N = bblist.Count;
				adjacency = new int[N, N];
				for (int i = 0; i < N; i++)
				{
					BasicBlock[] adj = { bblist[i].Target, bblist[i].Successor };
					for (int j = 0; j < N; j++)
					{
						adjacency[i, j] = (bblist[i].Target == bblist[j] || bblist[i].Successor == bblist[j]) ? 1 : (int)short.MaxValue;
					}
				}
				FloydWarshall();
			}
		}
	}
	
	public class IrreducibleCFGException : Exception
	{
		public IrreducibleCFGException(string message) :
			base(message)
		{
		}
	}
		
	public enum StatementTypes
	{
		INSTRUCTION,
		BRANCH,
		INFLOOP,
		BREAK
	}
	
	public abstract class TreeStatement
	{
		public abstract StatementTypes StatementType { get; }
	}
	
	public class InstructionStatement : TreeStatement
	{
		public override StatementTypes StatementType
		{
			get
			{
				return StatementTypes.INSTRUCTION;
			}
		}
		
		public BasicBlockInstruction Instruction { get; private set; }
		
		public InstructionStatement(BasicBlockInstruction instruction)
		{
			if (instruction == null)
				throw new ArgumentNullException("instruction");
			Instruction = instruction;
		}
	}
	
	public class BranchStatement : TreeStatement
	{
		public override StatementTypes StatementType
		{
			get
			{
				return StatementTypes.BRANCH;
			}
		}
		
		public VirtualRegister Flag { get; private set; }
		
		public IEnumerable<TreeStatement> TrueBranch { get; private set; }
		
		public IEnumerable<TreeStatement> FalseBranch { get; private set; }
		
		public BranchStatement(VirtualRegister flag, IEnumerable<TreeStatement> trueBranch, IEnumerable<TreeStatement> falseBranch = null)
		{
			if (flag == null)
				throw new ArgumentNullException("flag");
			
			Util.CheckArgumentType(typeof(int), flag.DataType, "flag");
			
			Flag = flag;
			
			if (trueBranch == falseBranch)
				throw new ArgumentException("Branch targets must be different", "falseBranch");
			
			TrueBranch = trueBranch;
			FalseBranch = falseBranch;
		}
	}
	
	public class InfiniteLoopStatement : TreeStatement
	{
		public override StatementTypes StatementType
		{
			get
			{
				return StatementTypes.INFLOOP;
			}
		}
		
		public IEnumerable<TreeStatement> Body { get; private set; }
		
		public InfiniteLoopStatement(IEnumerable<TreeStatement> body)
		{
			if (body == null)
				throw new ArgumentNullException("body");
			
			Body = body;
		}
	}
	
	public class BreakStatement : TreeStatement
	{
		public override StatementTypes StatementType
		{
			get
			{
				return StatementTypes.BREAK;
			}
		}
	}
}

