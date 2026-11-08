#!/bin/sh
# Downloads the CC0 sky and ground textures used by the Unity project (Poly Haven, https://polyhaven.com/license).
D="$(dirname "$0")/../unity/Assets/_Kryvon/ThirdParty/PolyHaven"
mkdir -p "$D"
B=https://dl.polyhaven.org/file/ph-assets
curl -sfL -o "$D/sky_partly_cloudy_2k.hdr" "$B/HDRIs/hdr/2k/kloofendal_48d_partly_cloudy_puresky_2k.hdr"
for t in asphalt_02 dry_ground_01 coast_sand_01 cobblestone_floor_08 beige_wall_001 plastered_wall_02 painted_plaster_wall concrete_wall_008 floor_tiles_06 laminate_floor_02 concrete_floor_02 clay_roof_tiles stone_tiles_02; do
  curl -sfL -o "$D/${t}_diff.jpg" "$B/Textures/jpg/2k/$t/${t}_diff_2k.jpg"
  curl -sfL -o "$D/${t}_nor.jpg"  "$B/Textures/jpg/2k/$t/${t}_nor_gl_2k.jpg"
done
echo "Poly Haven assets (CC0 1.0): kloofendal_48d_partly_cloudy_puresky, asphalt_02, dry_ground_01, coast_sand_01, cobblestone_floor_08, beige_wall_001, plastered_wall_02, painted_plaster_wall, concrete_wall_008, floor_tiles_06, laminate_floor_02, concrete_floor_02, clay_roof_tiles, stone_tiles_02" > "$D/LICENSE.txt"
ls -la "$D"
