using UnityEngine;

namespace OceanMotion.Subproject05
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class WaveSurface : MonoBehaviour
    {
        [SerializeField] private WaveField field;
        [SerializeField] private Vector2 size = new Vector2(20f, 20f);
        [SerializeField] private Vector2Int resolution = new Vector2Int(41, 41);
        [SerializeField] private bool recalculateNormalsEveryFrame = true;

        private Mesh surfaceMesh;
        private Vector3[] vertices;

        public WaveField Field
        {
            get => field;
            set => field = value;
        }

        public Vector2 Size
        {
            get => size;
            set => size = value;
        }

        public Vector2Int Resolution
        {
            get => resolution;
            set => resolution = value;
        }

        private void Awake()
        {
            ResolveField();
            RebuildMesh();
        }

        private void Update()
        {
            UpdateSurface(Time.time);
        }

        public void RebuildMesh()
        {
            int columns = Mathf.Max(2, resolution.x);
            int rows = Mathf.Max(2, resolution.y);
            resolution = new Vector2Int(columns, rows);
            size.x = Mathf.Max(0.01f, size.x);
            size.y = Mathf.Max(0.01f, size.y);

            ReleaseMesh();

            surfaceMesh = new Mesh
            {
                name = "SP05 Runtime Water Surface"
            };
            surfaceMesh.MarkDynamic();

            vertices = new Vector3[columns * rows];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[(columns - 1) * (rows - 1) * 6];

            for (int row = 0; row < rows; row++)
            {
                float rowAmount = row / (float)(rows - 1);
                float localZ = Mathf.Lerp(-size.y * 0.5f, size.y * 0.5f, rowAmount);

                for (int column = 0; column < columns; column++)
                {
                    float columnAmount = column / (float)(columns - 1);
                    float localX = Mathf.Lerp(
                        -size.x * 0.5f,
                        size.x * 0.5f,
                        columnAmount
                    );
                    int vertexIndex = row * columns + column;

                    vertices[vertexIndex] = new Vector3(localX, 0f, localZ);
                    uv[vertexIndex] = new Vector2(columnAmount, rowAmount);
                }
            }

            int triangleIndex = 0;

            for (int row = 0; row < rows - 1; row++)
            {
                for (int column = 0; column < columns - 1; column++)
                {
                    int bottomLeft = row * columns + column;
                    int topLeft = bottomLeft + columns;

                    triangles[triangleIndex++] = bottomLeft;
                    triangles[triangleIndex++] = topLeft;
                    triangles[triangleIndex++] = bottomLeft + 1;
                    triangles[triangleIndex++] = bottomLeft + 1;
                    triangles[triangleIndex++] = topLeft;
                    triangles[triangleIndex++] = topLeft + 1;
                }
            }

            surfaceMesh.vertices = vertices;
            surfaceMesh.uv = uv;
            surfaceMesh.triangles = triangles;
            surfaceMesh.RecalculateNormals();
            surfaceMesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = surfaceMesh;
        }

        public void UpdateSurface(float simulationTime)
        {
            ResolveField();

            if (field == null || surfaceMesh == null || vertices == null)
            {
                return;
            }

            for (int index = 0; index < vertices.Length; index++)
            {
                Vector3 localVertex = vertices[index];
                Vector3 worldSamplePosition = transform.TransformPoint(
                    new Vector3(localVertex.x, 0f, localVertex.z)
                );
                float worldHeight = field.SampleHeight(
                    worldSamplePosition,
                    simulationTime
                );
                Vector3 targetWorldPosition = new Vector3(
                    worldSamplePosition.x,
                    worldHeight,
                    worldSamplePosition.z
                );

                localVertex.y = transform.InverseTransformPoint(targetWorldPosition).y;
                vertices[index] = localVertex;
            }

            surfaceMesh.vertices = vertices;

            if (recalculateNormalsEveryFrame)
            {
                surfaceMesh.RecalculateNormals();
            }

            surfaceMesh.RecalculateBounds();
        }

        private void ResolveField()
        {
            if (field == null)
            {
                field = GetComponent<WaveField>();
            }
        }

        private void OnDestroy()
        {
            ReleaseMesh();
        }

        private void ReleaseMesh()
        {
            if (surfaceMesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(surfaceMesh);
            }
            else
            {
                DestroyImmediate(surfaceMesh);
            }

            surfaceMesh = null;
            vertices = null;
        }
    }
}
