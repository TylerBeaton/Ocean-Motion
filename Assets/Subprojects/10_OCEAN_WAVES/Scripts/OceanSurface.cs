using OceanMotion.Subproject05;
using UnityEngine;

namespace OceanMotion.Subproject10
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(WaveField))]
    public sealed class OceanSurface : MonoBehaviour
    {
        private enum WaveModel
        {
            ExistingWaves,
            [InspectorName("JONSWAP")] Jonswap
        }

        private const float PatchSize = 72f;
        private const int Resolution = 97;
        private const float MinimumPeakPeriod = 3.2f;

        [SerializeField] private WaveModel waveModel = WaveModel.ExistingWaves;

        [Header("JONSWAP")]
        [SerializeField, Min(0f)] private float significantWaveHeight = 1.2f;
        [SerializeField, Min(MinimumPeakPeriod)] private float peakPeriod = 3.6f;
        [SerializeField, Tooltip("Travel direction in degrees clockwise from world +Z toward +X.")]
        private float travelHeading = 25f;
        [SerializeField] private int seed = 10;

        private MeshFilter meshFilter;
        private WaveField existingWaveField;
        private WaveComponent[] spectrumWaves;
        private Mesh mesh;
        private Vector3[] samplePositions;
        private Vector3[] vertices;

        public float SampleHeight(Vector3 worldPosition, float simulationTime)
        {
            if (existingWaveField == null)
            {
                existingWaveField = GetComponent<WaveField>();
            }

            if (waveModel == WaveModel.ExistingWaves)
            {
                return existingWaveField.SampleHeight(worldPosition, simulationTime);
            }

            var waves = spectrumWaves ??= JonswapSpectrum.CreateWaves(
                Mathf.Max(0f, significantWaveHeight), Mathf.Max(MinimumPeakPeriod, peakPeriod), travelHeading, seed);
            float neutralHeight = transform.position.y + existingWaveField.BaseHeightOffset;
            return WaveSampler.SampleHeight(
                new Vector2(worldPosition.x, worldPosition.z), simulationTime, neutralHeight, waves);
        }

        private void OnValidate() => spectrumWaves = null;

        private void OnEnable()
        {
            meshFilter = GetComponent<MeshFilter>();
            BuildGrid();
            Update();
        }

        private void Update()
        {
            float simulationTime = Application.IsPlaying(gameObject) ? Time.time : 0f;
            Matrix4x4 localToWorld = transform.localToWorldMatrix;
            Matrix4x4 worldToLocal = transform.worldToLocalMatrix;
            for (int index = 0; index < vertices.Length; index++)
            {
                Vector3 worldPoint = localToWorld.MultiplyPoint3x4(samplePositions[index]);
                worldPoint.y = SampleHeight(worldPoint, simulationTime);
                vertices[index] = worldToLocal.MultiplyPoint3x4(worldPoint);
            }

            mesh.SetVertices(vertices);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private void OnDisable()
        {
            if (mesh == null)
            {
                return;
            }

            if (meshFilter != null && meshFilter.sharedMesh == mesh)
            {
                meshFilter.sharedMesh = null;
            }

            if (Application.IsPlaying(gameObject))
            {
                Destroy(mesh);
            }
            else
            {
                DestroyImmediate(mesh);
            }

            mesh = null;
        }

        private void BuildGrid()
        {
            vertices = new Vector3[Resolution * Resolution];
            samplePositions = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[(Resolution - 1) * (Resolution - 1) * 6];
            int triangle = 0;

            for (int row = 0; row < Resolution; row++)
            {
                for (int column = 0; column < Resolution; column++)
                {
                    int index = row * Resolution + column;
                    float u = column / (float)(Resolution - 1);
                    float v = row / (float)(Resolution - 1);
                    vertices[index] = new Vector3((u - 0.5f) * PatchSize, 0f, (v - 0.5f) * PatchSize);
                    samplePositions[index] = vertices[index];
                    uvs[index] = new Vector2(u, v);

                    if (row == Resolution - 1 || column == Resolution - 1)
                    {
                        continue;
                    }

                    triangles[triangle++] = index;
                    triangles[triangle++] = index + Resolution;
                    triangles[triangle++] = index + 1;
                    triangles[triangle++] = index + 1;
                    triangles[triangle++] = index + Resolution;
                    triangles[triangle++] = index + Resolution + 1;
                }
            }

            mesh = new Mesh { name = "SP10 Ocean Grid", hideFlags = HideFlags.HideAndDontSave };
            mesh.MarkDynamic();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            meshFilter.sharedMesh = mesh;
        }
    }
}
