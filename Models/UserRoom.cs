namespace RetroRec_Server.Models;

public class UserRoom
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int CreatorAccountId { get; set; }
    public int BaseRoomId { get; set; }       // which base room template was cloned
    public string UnitySceneId { get; set; } = "";
    public string ImageName { get; set; } = "";
    public int Accessibility { get; set; }    // 0=Public, 1=Listed, 2=Unlisted/Private
    public bool IsPublished { get; set; }     // false = private draft, true = on community list
    public string DataBlob { get; set; } = "";  // serialized scene contents (maker pen state)
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
}
