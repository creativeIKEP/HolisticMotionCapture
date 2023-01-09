using NUnit.Framework;
using UniJSON;
using System.Linq;

namespace UniGLTF
{
    public class GltfJsonUtilTests
    {
        [Test]
        public void Update_extensionsUsed()
        {
            var dst = GltfJsonUtil.FindUsedExtensionsAndUpdateJson(@"{
    ""asset"": {
        ""generator"": ""COLLADA2GLTF"",
        ""version"": ""2.0""
    },
    ""scene"": 0,
    ""scenes"": [
        {
            ""nodes"": [
                0
            ]
    }
    ],
    ""materials"": [
        {
            ""pbrMetallicRoughness"": {
                ""baseColorFactor"": [
                    0.800000011920929,
                    0.0,
                    0.0,
                    1.0
                ],
                ""metallicFactor"": 0.0
            },
            ""name"": ""Red"",
            ""extensions"": {
                ""KHR_materials_unlit"": {}
            }
        }
    ]
}");

            var parsed = dst.ParseAsJson();

            Assert.AreEqual(new string[] { "KHR_materials_unlit" },
            parsed[GltfJsonUtil.EXTENSION_USED_KEY].ArrayItems().Select(x => x.GetString()).ToArray());
        }

        [Test]
        public void Replace_extensionsUsed()
        {
            var dst = GltfJsonUtil.FindUsedExtensionsAndUpdateJson(@"{
    ""asset"": {
        ""generator"": ""COLLADA2GLTF"",
        ""version"": ""2.0""
    },
    ""scene"": 0,
    ""scenes"": [
        {
            ""nodes"": [
                0
            ]
    }
    ],
    ""extensionsUsed"": [""dummy""],
    ""materials"": [
        {
            ""pbrMetallicRoughness"": {
                ""baseColorFactor"": [
                    0.800000011920929,
                    0.0,
                    0.0,
                    1.0
                ],
                ""metallicFactor"": 0.0
            },
            ""name"": ""Red"",
            ""extensions"": {
                ""KHR_materials_unlit"": {}
            }
        }
    ]
}");

            var parsed = dst.ParseAsJson();

            Assert.AreEqual(new string[] { "KHR_materials_unlit" },
            parsed[GltfJsonUtil.EXTENSION_USED_KEY].ArrayItems().Select(x => x.GetString()).ToArray());
        }

        [Test]
        public void Empty_extensionsUsed()
        {
            var dst = GltfJsonUtil.FindUsedExtensionsAndUpdateJson(@"{
    ""asset"": {
        ""generator"": ""COLLADA2GLTF"",
        ""version"": ""2.0""
    },
    ""scene"": 0,
    ""scenes"": [
        {
            ""nodes"": [
                0
            ]
    }
    ],
    ""extensionsUsed"": [""dummy""]  ,  
    ""materials"": [
        {
            ""pbrMetallicRoughness"": {
                ""baseColorFactor"": [
                    0.800000011920929,
                    0.0,
                    0.0,
                    1.0
                ],
                ""metallicFactor"": 0.0
            },
            ""name"": ""Red""
        }
    ]
}");

            var parsed = dst.ParseAsJson();
            Assert.False(parsed.ContainsKey(GltfJsonUtil.EXTENSION_USED_KEY));
        }

        [Test]
        public void Empty2_extensionsUsed()
        {
            var dst = GltfJsonUtil.FindUsedExtensionsAndUpdateJson(@"{
    ""asset"": {
        ""generator"": ""COLLADA2GLTF"",
        ""version"": ""2.0""
    },
    ""scene"": 0,
    ""scenes"": [
        {
            ""nodes"": [
                0
            ]
    }
    ],
    ""materials"": [
        {
            ""pbrMetallicRoughness"": {
                ""baseColorFactor"": [
                    0.800000011920929,
                    0.0,
                    0.0,
                    1.0
                ],
                ""metallicFactor"": 0.0
            },
            ""name"": ""Red""
        }
    ],
    ""extensionsUsed"": [""dummy""]
}");

            var parsed = dst.ParseAsJson();
            Assert.False(parsed.ContainsKey(GltfJsonUtil.EXTENSION_USED_KEY));
        }
    }
}
