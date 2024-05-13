using Godot;
namespace Lokrian
{
	public partial class Terrain : StaticBody3D
	{

		private RandomNumberGenerator rng = new RandomNumberGenerator();
		private Octree<Node> AwesomeTerrain = new Octree<Node>(Vector3.Zero, Vector3.One * 1000);
		private GodotThread thread = new();

		public override void _Ready()
		{
			rng.Randomize();
			thread.Start(new Callable(this, nameof(BackgroundInsert)));
		}

		private void BackgroundInsert()
		{
			for (int i = 0; i < 1000000; i++)
			{
				// Generate a random position within the bounds of 0 to 1000 in each dimension
				var position = new Vector3(rng.RandfRange(0, 1000), rng.RandfRange(0, 1000), rng.RandfRange(0, 1000));
				var node = new Node();

				CallDeferred("InsertNode", position, node);
			}
		}

		private void InsertNode(Vector3 position, Node node)
		{
			AwesomeTerrain.Insert(position, node);
		}

		public override void _ExitTree()
		{
			// Ensure the thread finishes before the node is freed
			thread.WaitToFinish();
		}
	}
}
