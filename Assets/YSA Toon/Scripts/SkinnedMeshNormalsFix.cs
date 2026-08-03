using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class SkinnedMeshNormalsFix : MonoBehaviour
{

    private SkinnedMeshRenderer skinnedTarget;

    void Start()
    {
        skinnedTarget = GetComponent<SkinnedMeshRenderer>();

        Mesh mesh = Instantiate(skinnedTarget.sharedMesh);

        var repeated = mesh.vertices
                            .Select((v, i) => new { Value = v, Index = i })
                               .GroupBy(x => x.Value)
                               .Select(x => new
                               {
                                   Vector = x.Key,
                                   Index = x.ToList()
                               })
                               .ToList();

        Color[] colors = new Color[mesh.vertices.Length];

        for (int i = 0; i < repeated.Count; i++)
        {
            Vector3 normal = Vector3.zero;
            int[] rep = new int[repeated[i].Index.Count];

            for (int x = 0; x < rep.Length; x++)
            {
                rep[x] = repeated[i].Index[x].Index;
                normal += mesh.normals[rep[x]];
            }

            for (int x = 0; x < rep.Length; x++)
            {
                Vector3 nn = Vector3.Normalize(normal);
                colors[rep[x]] = new Color(nn.x, nn.y, nn.z);
            }
        }

        mesh.colors = colors;

        skinnedTarget.sharedMesh = mesh;
    }

}
