using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kryvon.EditorTools
{
    public static class KryvonQaia
    {
        const string Root = "Assets/_Kryvon";

        public static void BuildAt(Transform parent, Terrain terrain, Vector2 region)
        {
            float y = terrain.SampleHeight(new Vector3(region.x, 0, region.y)) + terrain.transform.position.y;
            BuildInto(parent, terrain, new Vector3(region.x, y, region.y));
        }

        [MenuItem("KRYVON/World/Build QAIA airport")]
        public static void Build()
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                Debug.LogError("KRYVON QAIA: open the Levant scene and create Terrain first.");
                return;
            }
            var region = KryvonLevant.RegionPos("QAIA");
            var centre = new Vector3(region.x, terrain.SampleHeight(new Vector3(region.x, 0, region.y)) + terrain.transform.position.y, region.y);
            BuildInto(null, terrain, centre);
        }

        static void BuildInto(Transform externalParent, Terrain terrain, Vector3 centre)
        {
            var root = new GameObject("QAIA").transform;
            if (externalParent != null) root.SetParent(externalParent, false);
            root.position = centre;

            var concrete = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "QAIA Concrete", color = new Color(0.78f, 0.76f, 0.70f) };
            var glass = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "QAIA Glass", color = new Color(0.30f, 0.48f, 0.56f, 0.78f) };
            glass.SetFloat("_Smoothness", 0.95f); glass.SetFloat("_Surface", 1);
            var asphalt = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "QAIA Asphalt", color = new Color(0.16f, 0.17f, 0.17f) };
            var yellow = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "QAIA Yellow", color = new Color(0.95f, 0.72f, 0.12f) };
            var blue = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "QAIA Blue", color = new Color(0.05f, 0.30f, 0.58f) };

            // flat aprons and two QAIA-scale parallel runways
            Box(root, "Runway 08L/26R", new Vector3(-40, 0.05f, 0), new Vector3(520, 0.10f, 58), asphalt);
            Box(root, "Runway 08R/26L", new Vector3(40, 0.05f, 220), new Vector3(520, 0.10f, 58), asphalt);
            Box(root, "Main Apron", new Vector3(0, 0.08f, -105), new Vector3(420, 0.12f, 95), asphalt);
            for (int i = -4; i <= 4; i++)
            {
                Box(root, "RunwayCenterLine", new Vector3(i * 54, 0.12f, 220), new Vector3(22, 0.02f, 1.2f), yellow);
                Box(root, "TaxiwayMark", new Vector3(i * 54, 0.12f, 0), new Vector3(22, 0.02f, 1.2f), yellow);
            }

            // traveler terminal: six-by-four interlocking concrete dome modules
            var terminal = new GameObject("PassengerTerminal").transform;
            terminal.SetParent(root, false); terminal.localPosition = new Vector3(0, 0, -145);
            for (int rz = 0; rz < 4; rz++)
                for (int rx = -3; rx <= 3; rx++)
                {
                    float x = rx * 20f, z = rz * 18f;
                    var dome = new GameObject($"Dome_{rx}_{rz}").transform;
                    dome.SetParent(terminal, false); dome.localPosition = new Vector3(x, 0, z);
                    var baseMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    baseMesh.name = "ConcreteBranch";
                    baseMesh.transform.SetParent(dome, false);
                    baseMesh.transform.localPosition = new Vector3(0, 6.5f, 0);
                    baseMesh.transform.localScale = new Vector3(1.1f, 13f, 1.1f);
                    baseMesh.GetComponent<Renderer>().sharedMaterial = concrete;
                    Object.DestroyImmediate(baseMesh.GetComponent<Collider>());
                    var top = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    top.name = "SignatureDome";
                    top.transform.SetParent(dome, false);
                    top.transform.localPosition = new Vector3(0, 14f, 0);
                    top.transform.localScale = new Vector3(18f, 7.5f, 18f);
                    top.GetComponent<Renderer>().sharedMaterial = concrete;
                    Object.DestroyImmediate(top.GetComponent<Collider>());
                    var skylight = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    skylight.name = "Skylight";
                    skylight.transform.SetParent(dome, false);
                    skylight.transform.localPosition = new Vector3(0, 18f, 0);
                    skylight.transform.localScale = new Vector3(5.5f, 0.12f, 5.5f);
                    skylight.GetComponent<Renderer>().sharedMaterial = glass;
                    Object.DestroyImmediate(skylight.GetComponent<Collider>());
                }
            Box(terminal, "ArrivalsHall", new Vector3(0, 2.5f, -42), new Vector3(145, 5, 28), concrete);
            Box(terminal, "DeparturesHall", new Vector3(0, 8, 34), new Vector3(145, 5, 28), concrete);
            for (int i = -4; i <= 4; i++)
            {
                Box(terminal, "TicketCounter", new Vector3(i * 14, 1.0f, 12), new Vector3(9, 2, 2), blue);
                Box(terminal, "BaggageCarousel", new Vector3(i * 14, 0.8f, -28), new Vector3(9, 1.6f, 2.2f), concrete);
            }

            // airside piers and jet bridges
            for (int side = -1; side <= 1; side += 2)
            {
                var pier = new GameObject(side < 0 ? "NorthPier" : "SouthPier").transform;
                pier.SetParent(root, false);
                pier.localPosition = new Vector3(side * 155, 0, -105);
                Box(pier, "PierBody", new Vector3(0, 3, 0), new Vector3(28, 6, 105), glass);
                for (int gate = 101; gate <= 112; gate++)
                {
                    float z = -42 + (gate - 101) * 7.5f;
                    Box(pier, $"Gate{gate}", new Vector3(side * 2, 3.2f, z), new Vector3(10, 2.2f, 4.2f), blue);
                    Box(pier, $"JetBridge{gate}", new Vector3(side * 20, 2.8f, z), new Vector3(30, 2.2f, 3.2f), glass);
                }
            }

            // ATC tower: tall glazed cabin with internal stair and sniper platform
            var tower = new GameObject("ATCTower").transform;
            tower.SetParent(root, false); tower.localPosition = new Vector3(180, 0, -65);
            Box(tower, "TowerShaft", new Vector3(0, 18, 0), new Vector3(11, 36, 11), concrete);
            Box(tower, "TowerCabin", new Vector3(0, 38, 0), new Vector3(18, 5, 18), glass);
            Box(tower, "SniperPlatform", new Vector3(0, 41, 0), new Vector3(22, 0.5f, 22), concrete);
            for (int i = 0; i < 25; i++)
                Box(tower, "ATCStair", new Vector3((i % 2 == 0 ? -3.5f : 3.5f), 1.3f + i * 1.35f, 0), new Vector3(5.5f, 0.28f, 1.6f), concrete);

            // hangars, cargo, fuel and aprons
            for (int i = -2; i <= 2; i++)
            {
                var hangar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                hangar.name = i % 2 == 0 ? "MaintenanceHangar" : "CargoHangar";
                hangar.transform.SetParent(root, false);
                hangar.transform.localPosition = new Vector3(i * 90, 7, 110);
                hangar.transform.localScale = new Vector3(64, 14, 38);
                hangar.GetComponent<Renderer>().sharedMaterial = concrete;
            }
            for (int i = 0; i < 12; i++)
                Box(root, "ParkingSpot", new Vector3(-250 + i * 36, 0.1f, -230), new Vector3(28, 0.03f, 12), i % 3 == 0 ? yellow : asphalt);

            // parked aircraft and vehicles
            foreach (var x in new[] { -90f, 0f, 90f })
            {
                var aircraft = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                aircraft.name = "ParkedPassengerAircraft";
                aircraft.transform.SetParent(root, false);
                aircraft.transform.localPosition = new Vector3(x, 4, -78);
                aircraft.transform.localScale = new Vector3(12, 12, 28);
                aircraft.transform.localRotation = Quaternion.Euler(90, 0, 0);
                aircraft.GetComponent<Renderer>().sharedMaterial = glass;
            }
            foreach (var x in new[] { -220f, -120f, 120f, 220f })
            {
                var bus = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bus.name = "AirportShuttle";
                bus.transform.SetParent(root, false);
                bus.transform.localPosition = new Vector3(x, 2.2f, -210);
                bus.transform.localScale = new Vector3(12, 4.4f, 28);
                bus.GetComponent<Renderer>().sharedMaterial = blue;
            }

            var signRoot = new GameObject("QAIA Signs").transform;
            signRoot.SetParent(root, false);
            for (int i = -3; i <= 3; i++)
            {
                Box(signRoot, i < 0 ? "DEPARTURES" : i > 0 ? "ARRIVALS" : "QAIA", new Vector3(i * 32, 7, -160), new Vector3(24, 4, 0.5f), i == 0 ? yellow : blue);
            }

            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("KRYVON: QAIA airport built with terminal domes, two runways, piers, jet bridges, ATC tower, hangars and parking.");
        }

        static void Box(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = localScale;
            box.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
