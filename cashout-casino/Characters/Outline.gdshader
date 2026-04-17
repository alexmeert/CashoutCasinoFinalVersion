shader_type spatial;
render_mode unshaded, blend_premul_alpha, depth_draw_never;

uniform sampler2D depth_texture : hint_depth_texture;

uniform int step_count : hint_range(3, 15, 2) = 5;
uniform float thickness : hint_range(1.0, 16.0, 0.1) = 4.0;
uniform vec3 edge_color : source_color = vec3(0.0);

void fragment() {

    vec2 uv = SCREEN_UV;

    vec2 texel = 1.0 / VIEWPORT_SIZE * thickness;

    float edge = 0.0;

    // simple 4-direction depth difference (more stable than your radial loop)
    float d  = texture(depth_texture, uv).r;
    float d1 = texture(depth_texture, uv + vec2(texel.x, 0)).r;
    float d2 = texture(depth_texture, uv - vec2(texel.x, 0)).r;
    float d3 = texture(depth_texture, uv + vec2(0, texel.y)).r;
    float d4 = texture(depth_texture, uv - vec2(0, texel.y)).r;

    edge += abs(d - d1);
    edge += abs(d - d2);
    edge += abs(d - d3);
    edge += abs(d - d4);

    edge = smoothstep(0.01, 0.05, edge);

    ALBEDO = edge_color;
    ALPHA = edge;
}