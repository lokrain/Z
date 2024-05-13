
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using Array = Godot.Collections.Array;

namespace Lokrian
{
    public partial class Octree<[MustBeVariant] T> : Node
    {
        [Export]
        public int MaxEntitiesOnLeaf { get => _maxEntitiesOnLeaf; set => _maxEntitiesOnLeaf = value; }
        private int _maxEntitiesOnLeaf = 512;

        public Octree<T> Parent = default;
        public Octree<T>[] Children = default;
        public Vector3 Position { get; private set; }
        public Vector3 Size { get; private set; }

        [Export]
        public Array<T> Data { get; private set; }
        private Array<T> _data = new();

        [Export]
        public bool IsLeaf { get => _isLeaf; set => _isLeaf = value; }
        private bool _isLeaf = false;


        [Export]
        public bool IsBranch { get => _isBranch; set => _isBranch = value; }
        private bool _isBranch = false;

        [Export]
        public bool IsRoot { get => _isRoot; set => _isRoot = value; }
        private bool _isRoot = false;

        private int _depth = 0;
        private int _indexAt(Vector3 position)
        {
            return (position.X <= Position.X ? 0 : 1) + (position.Y <= Position.Y ? 0 : 2) + (position.Z <= Position.Z ? 0 : 4);
        }

        public Octree(Vector3 position, Vector3 size)
        {
            Position = position;
            Size = size;
            Children = new Octree<T>[8];
        }

        public bool Insert(Vector3 position, T data)
        {
            GD.Print("Inserting data.");

            // Use-case: Inserting data into the root node.
            if (_isLeaf && Data.Count == 0)
            {
                GD.Print("Inserting data into the root node.");
                Data = new Array<T>();

                _isRoot = true;

                return true;
            }

            // Use-case: Inserting data into existing leaf node.
            if (_isLeaf && Position.Equals(position))
            {
                GD.Print("Inserting data into existing leaf node.");
                Data.Add(data);

                return true;
            }

            // Use-case: Inserting data into a leaf node that has enough data to split.
            if (_isLeaf && !Position.Equals(position))
            {
                GD.Print("Inserting data into a leaf node that has enough data to split.");
                Split();
                Insert(position, data);

                return true;
            }

            // Use-case: Inserting data into a branch node.
            if (_isBranch)
            {
                GD.Print("Inserting data into a branch node.");
                Children[_indexAt(position)].Insert(position, data);

                return true;
            }

            GD.Print("Inserting data failed.");
            return false;
        }

        public Array<T> Retrieve(Vector3 position)
        {
            if (_isLeaf && Position.Equals(position))
                return Data;

                if (_isBranch)
                return Children[_indexAt(position)].Retrieve(position);

            return default;
        }

        private bool CanSplit()
        {
            return _isLeaf && Data.Count > MaxEntitiesOnLeaf;
        }

        private void Split()
        {
            if (!CanSplit())
                return;

            Vector3 half = Size / 2;
            Vector3 childSize = half;

            GD.Print("Splitting node.");

            for (int i = 0; i < 8; i++)
            { 
                Vector3 childPosition = Position + new Vector3(
                    (i & 1) == 0 ? 0 : half.X,
                    (i & 2) == 0 ? 0 : half.Y,
                    (i & 4) == 0 ? 0 : half.Z
                );

                Children[i] = new Octree<T>(childPosition, childSize)
                {
                    Parent = this,
                    _isLeaf = true,
                    _depth = _depth + 1
                };

                GD.Print($"Child {i} created at {childPosition}.");
            }

            if (Data.Count > 0)
            {
                foreach (var data in Data)
                {
                    Children[_indexAt(Position)].Insert(Position, data);
                }
            }

            Data = default;
            _isLeaf = false;
            _isBranch = true;
        }

        public void Clear()
        {
            if (_isBranch)
            {
                foreach (var child in Children)
                {
                    child.Clear();
                }
            }

            Data = default;
        }

        public void Draw(MeshInstance3D meshInstance3D)
        {
            if (_isBranch)
            {
                foreach (var child in Children)
                {
                    child.Draw(meshInstance3D);
                }
            }

            if (_isLeaf)
            {
                var mesh = new ArrayMesh();
                var vertices = new Array();
                var indices = new Array();
                var colors = new Array();

                foreach (var data in Data)
                {
                    vertices.Add(Position);
                    colors.Add(new Color(1, 1, 1));
                }

                Array arrays = new();

                arrays.Resize((int)Mesh.ArrayType.Max);

                arrays[(int)Mesh.ArrayType.Vertex] = vertices;
                arrays[(int)Mesh.ArrayType.Index] = indices;
                arrays[(int)Mesh.ArrayType.Color] = colors;

                mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Points, arrays);

                meshInstance3D.Mesh = mesh;
            }
        }

        private Godot.Collections.Dictionary<Godot.Range, Color> _map = new()
        {
            // Looking like Obsidian
            { new Godot.Range { MinValue = 0, MaxValue = 0.05 }, Color.FromOkHsl(198, 0.11f, 0.30f)},
 
             // Water - Deep blue
            { new Godot.Range { MinValue = 0, MaxValue = 0.15 }, Color.FromOkHsl(210, 0.60f, 0.50f)},
            
            // Sand - Light yellow, starts under water level, overlaps with dirt
            { new Godot.Range { MinValue = 0.10, MaxValue = 0.25 }, Color.FromOkHsl(50, 0.70f, 0.90f)},
            
            // Dirt - Brown, starts below sand, overlaps with grass
            { new Godot.Range { MinValue = 0.20, MaxValue = 0.40 }, Color.FromOkHsl(30, 0.75f, 0.30f)},
            
            // Grass - Green, starts within dirt, overlaps slightly with stone
            { new Godot.Range { MinValue = 0.35, MaxValue = 0.45 }, Color.FromOkHsl(120, 0.50f, 0.50f)},
            
            // Stone - Grey, starts within grass range, extends into deeper layers
            { new Godot.Range { MinValue = 0.40, MaxValue = 0.65 }, Color.FromOkHsl(0, 0.0f, 0.50f)},
            
            // Coal - Dark grey, found within stone layers
            { new Godot.Range { MinValue = 0.50, MaxValue = 0.75 }, Color.FromOkHsl(0, 0.0f, 0.30f)},
            
            // Iron - Light grey, starts in stone, overlaps with gold
            { new Godot.Range { MinValue = 0.65, MaxValue = 0.90 }, Color.FromOkHsl(0, 0.0f, 0.70f)},
            
            // Gold - Gold, overlaps with iron
            { new Godot.Range { MinValue = 0.85, MaxValue = 1.00 }, Color.FromOkHsl(47, 0.80f, 0.70f)},
        };

    }

    public partial class BenchmarkResult : Node
    {
        internal int Iterations;

        public TimeSpan Duration { get; set; }
    }

    public class Benchmark<[MustBeVariant] TContext, [MustBeVariant] TBenchmarkResult> where TBenchmarkResult : BenchmarkResult, new()
    {
        public static ConcurrentBag<TBenchmarkResult> Results = new();

        public static void Run(Action<CancellationToken> action, string name, int maxConcurrency = 4, int iterations = 10)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var options = new ParallelOptions
            {
                CancellationToken = cancellationTokenSource.Token,
                MaxDegreeOfParallelism = maxConcurrency
            };



            try
            {
                Parallel.For(0, iterations, options, i =>
                {
                    var stopwatch = Stopwatch.StartNew();
                    action(options.CancellationToken);
                    stopwatch.Stop();
                    Results.Add(new TBenchmarkResult
                    {
                        Name = name,
                        Duration = stopwatch.Elapsed, // This captures the total elapsed time until this point, not per iteration
                        Iterations = 1
                    });
                });
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Benchmark cancelled.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during benchmarking: {ex.Message}");
            }
            finally
            {
                cancellationTokenSource.Dispose();
            }
        }

        private static void AggregateAndReportResults(IEnumerable<TBenchmarkResult> results)
        {
            // Aggregation and reporting logic here, depending on what metrics you need
        }


    }

}

